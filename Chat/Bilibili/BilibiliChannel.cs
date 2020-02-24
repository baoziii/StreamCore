using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Bilibili
{
    public class BilibiliChannel
    {
        public string name = "";
        public int roomId = 0;
        public List<BilibiliRoom> rooms = null;
        public BilibiliChannel(string channel)
        {
            name = channel;
        }
    }
}
