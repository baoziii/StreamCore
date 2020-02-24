using StreamCore.Chat;
using StreamCore.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamCore.Bilibili
{
    public class BilibiliMessage : GenericChatMessage
    {
        public string MessageType { get; set; } = "";
    }
}
