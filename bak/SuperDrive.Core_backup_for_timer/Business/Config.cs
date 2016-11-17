using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Helper;
using Newtonsoft.Json;
using NLog;
using Shareit.Foundation.Network;
using SuperDrive.Library;

namespace ConnectTo.Foundation.Business
{
    public static class TrustDeviceExtension
    {
        public static void AddOrReplace(this List<Device> devices,Device device)
        {
            //devices.RemoveAll(d => d.ID == device.ID);
            //devices.Add(device);

            var existingDevice = devices.FirstOrDefault(d => d.ID == device.ID);
            if (existingDevice != null)
            {
                existingDevice.CopyFrom(device);
            }
            else
            {
                devices.Add(device);
            }
        }
    }
    /// <summary>
    /// 配置信息
    /// </summary>
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class Config
    {
        internal static readonly string CONFIG_FILENAME = "connect2.config";
        [JsonProperty]
        Dictionary<string, string> _tempDB;
        public Dictionary<string, string> TempDB {
            get {
                if (_tempDB == null)
                {
                    _tempDB = new Dictionary<string, string>();
                }
                return _tempDB;
            }
            set
            {
                _tempDB = value;
            }
        } 
        [JsonProperty]
        public Device PairedDevice { get; set; }
        [JsonProperty]
        public List<Device> TrustDevices { get; set; }
        [JsonProperty]
        public string LastestIP { get; set; }
        [JsonProperty]
        public Device LocalDevice { get; set; }
        [JsonProperty]
        public bool SecureMode { get; set; }
        [JsonProperty]
        public bool MetricsEnabled { get; set; }
        [JsonProperty]
        public string SecureCode { get; set; }
        [JsonProperty]
        public string DefaultDir { get; set; }
        [JsonProperty]
        public string HotspotPassword { get; set; }
        [JsonProperty]
        public string HotspotName { get; set; }
        [JsonProperty]
        public string LastHotspotDeviceBtMac { get; set; }
        [JsonProperty]
        public string LastHotspotDeviceName{ get; set; }

        public string LogLocation { get; set; }
        public LogLevel LogLevel { get; set; }

        private string configFileName = null;

        

        public static Config Load(Env container, string fileName)
        {
            Preconditions.Check( !string.IsNullOrEmpty(fileName),"必须指定配置文件路径。");

            bool dirtdy = false;
            Config config = new Config();
            config.configFileName = fileName;

            string json = "";
            try
            {
                json = File.ReadAllText(fileName, Encoding.UTF8);
                JsonConvert.PopulateObject(json, config);
                
            }
            catch (Exception e)
            {
                dirtdy = true;
            }

            //检查反序列化后各个成员是否正确，如果不正确，配置默认值。
            //TODO DeviceInfo 序列化说明

            if (config.LocalDevice == null)
            {
                dirtdy = true;
                var device = new Device()
                {
                    Avatar = Avatar.Facebook,
                    Name = container.GetUserName(),// Environment.UserName, //TODO 
                    DeviceName = container.GetDeviceName(),// Environment.MachineName,
                    Version = "1.0",
                    DeviceType = DeviceType.PC
                };
                
                
                if (string.IsNullOrEmpty(device.Name) || device.Name.ToLower().Equals("somebody"))
                {
                    device.Name = device.DeviceName;
                }


                config.LocalDevice = device;
            }
            if(string.IsNullOrEmpty(config.LocalDevice.DeviceName))
            {
                config.LocalDevice.DeviceName = container.GetDeviceName();
            }
            if (config.LocalDevice.ID == null)
            {
                config.LocalDevice.ID = StringHelper.NewRandomGUID();
                dirtdy = true;
            }
            if(config.LogLevel == null)
            {
                config.LogLevel = LogLevel.Error;
                dirtdy = true;
            }
            if(config.TrustDevices == null)
            {
                config.TrustDevices = new List<Device>();
                dirtdy = true;
            }

            if (string.IsNullOrEmpty(config.DefaultDir) || !Directory.Exists(config.DefaultDir))
            {
                config.DefaultDir = container.GetRealPath(BrowseLocation.CONNECT2_DEFAULT);
                dirtdy = true;
            }

            if (dirtdy)
                config.Save();
            return config;
        }

        internal void SetDefaultValueIfNull(ref string key, string defaultValue,ref bool isDirty)
        {
            if(key == null)
            {
                key = defaultValue;
                isDirty = true;
            }
        }
        public void Save()
        {
            string json = JsonConvert.SerializeObject(this);
            try
            {
                File.WriteAllText(configFileName, json, Encoding.UTF8);
            }
            catch(Exception e)
            {
                throw e;
            }
            
        }
    }
}
