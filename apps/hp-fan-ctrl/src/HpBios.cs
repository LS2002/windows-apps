using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Text;

namespace HpFanCtrl
{
    internal sealed class HpBios : IDisposable
    {
        private const uint Command = 0x20008;
        private readonly object gate = new object();
        private ManagementScope scope;
        private ManagementObject methods;
        private ManagementClass dataClass;
        private string dataField = "hpqBData";
        private bool open;

        public string LastError { get; private set; }

        public bool IsOpen
        {
            get { return open; }
        }

        public bool Open()
        {
            LastError = null;
            try
            {
                ConnectionOptions options = new ConnectionOptions();
                options.EnablePrivileges = true;
                scope = new ManagementScope(@"\\.\root\wmi", options);
                scope.Connect();
                dataClass = new ManagementClass(scope, new ManagementPath("hpqBDataIn"), null);
                dataClass.Get();
                foreach (PropertyData property in dataClass.Properties)
                {
                    if (string.Equals(property.Name, "hpqBData", StringComparison.OrdinalIgnoreCase))
                    {
                        dataField = property.Name;
                    }
                }
                string instance = @"ACPI\PNP0C14\0_0".Replace("\\", "\\\\");
                methods = new ManagementObject(scope, new ManagementPath("hpqBIntM.InstanceName=\"" + instance + "\""), null);
                methods.Get();
                open = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                open = false;
                return false;
            }
        }

        public bool TryGetFanLevels(out byte cpu, out byte gpu, out int code)
        {
            cpu = 0;
            gpu = 0;
            byte[] data;
            if (!Call(0x2D, new byte[] { 0, 0, 0, 0 }, 128, out data, out code))
            {
                return false;
            }
            if (data == null || data.Length < 2)
            {
                LastError = "Fan level response was empty.";
                return false;
            }
            cpu = data[0];
            gpu = data[1];
            return true;
        }

        public bool TryGetFanRpm(int fan, out int rpm, out int code)
        {
            rpm = 0;
            byte index = (byte)fan;
            byte[] data;
            if (!Call(0x11, new byte[] { index, 0, 0, 0 }, 4, out data, out code))
            {
                return false;
            }
            if (data == null || data.Length < 4)
            {
                return false;
            }
            rpm = (data[2] << 8) | data[3];
            return rpm > 0 && rpm < 20000;
        }

        public bool TryGetBiosCelsius(out int celsius, out int code)
        {
            celsius = 0;
            byte[] data;
            if (!Call(0x23, new byte[] { 1, 0, 0, 0 }, 4, out data, out code))
            {
                return false;
            }
            if (data == null || data.Length < 1)
            {
                return false;
            }
            celsius = data[0];
            return celsius >= 1 && celsius <= 120;
        }

        public bool TrySetFanLevels(byte cpu, byte gpu, out int code)
        {
            byte[] ignored;
            int ignoredCode;
            Call(0x10, new byte[] { 0, 0, 0, 0 }, 4, out ignored, out ignoredCode);
            Call(0x27, new byte[] { 0, 0, 0, 0 }, 0, out ignored, out ignoredCode);
            return Call(0x2E, new byte[] { cpu, gpu, 0, 0 }, 0, out ignored, out code);
        }

        public static string Describe(int code, string fallback)
        {
            if (code == 0)
            {
                return "OK";
            }
            if (code == 3)
            {
                return "This HP BIOS does not accept fan-speed changes.";
            }
            if (code == 5)
            {
                return "The BIOS rejected the size of the fan command.";
            }
            if (code == 46)
            {
                return "This BIOS reports fan speed, but it will not accept a new speed.";
            }
            if (code == -1 && !string.IsNullOrEmpty(fallback))
            {
                return fallback;
            }
            return "BIOS returned code " + code.ToString(CultureInfo.InvariantCulture) + ".";
        }

        public void Dispose()
        {
            lock (gate)
            {
                open = false;
                if (methods != null)
                {
                    methods.Dispose();
                    methods = null;
                }
                if (dataClass != null)
                {
                    dataClass.Dispose();
                    dataClass = null;
                }
            }
        }

        public string ProbeReport()
        {
            StringBuilder report = new StringBuilder();
            if (!Open())
            {
                report.AppendLine("open FAIL");
                report.AppendLine(LastError ?? "unknown");
                return report.ToString();
            }
            report.AppendLine("open ok");
            report.AppendLine("dataField " + dataField);
            List<string> names = new List<string>();
            using (ManagementClass methodClass = new ManagementClass(scope, new ManagementPath("hpqBIntM"), null))
            {
                methodClass.Get();
                foreach (MethodData method in methodClass.Methods)
                {
                    names.Add(method.Name);
                }
            }
            report.AppendLine("methods " + string.Join(",", names.ToArray()));
            byte cpu;
            byte gpu;
            int code;
            if (TryGetFanLevels(out cpu, out gpu, out code))
            {
                report.AppendLine("levels " + cpu.ToString(CultureInfo.InvariantCulture) + "," + gpu.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                report.AppendLine("levels FAIL " + Describe(code, LastError));
            }
            int rpm;
            if (TryGetFanRpm(0, out rpm, out code))
            {
                report.AppendLine("rpm0 " + rpm.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                report.AppendLine("rpm0 FAIL " + Describe(code, LastError));
            }
            int temp;
            if (TryGetBiosCelsius(out temp, out code))
            {
                report.AppendLine("biosTemp " + temp.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                report.AppendLine("biosTemp FAIL " + Describe(code, LastError));
            }
            return report.ToString();
        }

        private static object Property(ManagementBaseObject source, string name)
        {
            if (source == null)
            {
                return null;
            }
            foreach (PropertyData property in source.Properties)
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
            return null;
        }

        private bool Call(uint commandType, byte[] input, int outSize, out byte[] output, out int code)
        {
            output = new byte[0];
            code = -1;
            LastError = null;
            lock (gate)
            {
            if (!open)
            {
                LastError = "BIOS interface is not open.";
                return false;
            }
            string methodName = "hpqBIOSInt" + outSize.ToString(CultureInfo.InvariantCulture);
            string step = "start";
            try
            {
                step = "input";
                ManagementObject inputObject = dataClass.CreateInstance();
                inputObject["Sign"] = new byte[] { 0x53, 0x45, 0x43, 0x55 };
                inputObject["Command"] = Command;
                inputObject["CommandType"] = commandType;
                inputObject["Size"] = (uint)(input == null ? 0 : input.Length);
                if (input != null && input.Length > 0)
                {
                    inputObject[dataField] = input;
                }
                step = "parameters";
                ManagementBaseObject parameters = methods.GetMethodParameters(methodName);
                parameters["InData"] = inputObject;
                step = "invoke";
                ManagementBaseObject result = methods.InvokeMethod(methodName, parameters, null);
                if (result == null)
                {
                    LastError = "BIOS call returned nothing.";
                    return false;
                }
                step = "result";
                object returnCode = Property(result, "rwReturnCode");
                if (returnCode != null)
                {
                    code = Convert.ToInt32(returnCode, CultureInfo.InvariantCulture);
                }
                ManagementBaseObject outObject = Property(result, "OutData") as ManagementBaseObject;
                if (outObject != null)
                {
                    object nestedCode = Property(outObject, "rwReturnCode");
                    if (nestedCode != null)
                    {
                        code = Convert.ToInt32(nestedCode, CultureInfo.InvariantCulture);
                    }
                    byte[] bytes = Property(outObject, "Data") as byte[];
                    if (bytes == null)
                    {
                        bytes = Property(outObject, "hpqBData") as byte[];
                    }
                    if (bytes != null)
                    {
                        output = bytes;
                    }
                }
                if (code != 0)
                {
                    LastError = Describe(code, LastError);
                }
                return code == 0;
            }
            catch (Exception ex)
            {
                LastError = step + ": " + ex.Message;
                code = -1;
                return false;
            }
            }
        }
    }
}
