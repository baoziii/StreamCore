using System;
using StreamCore.SimpleJSON;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StreamCore.Bilibili;

namespace StreamCore
{
    class Update
    {
        private static readonly string PluginUpdateInfo = "https://raw.githubusercontent.com/baoziii/StreamCore/master/version.json";
        private static readonly string LocalVersion = "2.2.8-Alpha2";
        private static readonly int LocalVersionNumber = 2282;
        public static BilibiliMessage updateMessageInfo = new BilibiliMessage();

        private static HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };

        public async static void GetPluginUpdateInfo()
        {
            Plugin.Log("Fetching Update Info...");
            updateMessageInfo.message = "";
            updateMessageInfo.messageType = "Update";
            try
            {
                var PluginInfo = JSONObject.Parse(await httpClient.GetStringAsync(PluginUpdateInfo));
                Plugin.Log("Update Info Downloaded");
                Plugin.Log(PluginInfo.ToString());
                if (Convert.ToInt32(PluginInfo["latest"]["VersionNumber"].Value) > LocalVersionNumber)
                {
                    updateMessageInfo.message = "<color=#FFFFC13C>【更新】StreamCore (bilibili edition) Plugin could update to " + PluginInfo["latest"]["Version"].Value + "!" + " What's New: " + PluginInfo["latest"]["ChangeLog"]["English"].Value + " StreamCore (bilibilil版)插件有新版本啦! 快升级到" + PluginInfo["latest"]["Version"].Value + "吧!" + "更新说明" + PluginInfo["latest"]["ChangeLog"]["ChineseSimplified"].Value + " Download at(下载地址): " + PluginInfo["latest"]["Download"].Value + "</color>";
                    Plugin.Log(updateMessageInfo.message);
                }
            }
            catch (Exception e){
                Plugin.Log(e.Message);
            }
        }

        public static string GetMessage() {
            return updateMessageInfo.message;
        }
    }
}
