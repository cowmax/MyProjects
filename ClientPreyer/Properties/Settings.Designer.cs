﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本:4.0.30319.42000
//
//     对此文件的更改可能会导致不正确的行为，并且如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
//------------------------------------------------------------------------------

namespace ClientPreyer.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "14.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://www.p1.cn/siteadmin/photographer/photographers.php")]
        public string phtgpherUrl {
            get {
                return ((string)(this["phtgpherUrl"]));
            }
            set {
                this["phtgpherUrl"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://www.p1.cn/reg/login.php")]
        public string loginUrl {
            get {
                return ((string)(this["loginUrl"]));
            }
            set {
                this["loginUrl"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://www.p1.cn/siteadmin/photographer/upload.php?act=edit&photographer_id={0}&p" +
            "u_id={1}\r\n")]
        public string clientDetailUrl {
            get {
                return ((string)(this["clientDetailUrl"]));
            }
            set {
                this["clientDetailUrl"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://www.p1.cn/siteadmin/photographer/photographers.php?act=all&order=asc&page=" +
            "{0}")]
        public string allPhtgpherUrl {
            get {
                return ((string)(this["allPhtgpherUrl"]));
            }
            set {
                this["allPhtgpherUrl"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("http://www.p1.cn/siteadmin/photographer/photo.php?act=photographer&photographer_i" +
            "d={0}&page={1}")]
        public string clientBaseUrl {
            get {
                return ((string)(this["clientBaseUrl"]));
            }
            set {
                this["clientBaseUrl"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("5")]
        public string intervalTime {
            get {
                return ((string)(this["intervalTime"]));
            }
            set {
                this["intervalTime"] = value;
            }
        }
    }
}
