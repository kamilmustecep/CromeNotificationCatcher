using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushMining.Models
{
    public class NotificationModel
    {
        public string brandName { get; set; }
        public string url { get; set; }
        public DateTime time { get; set; }
    }

    public class NotificationFullModel
    {
        public string brandName { get; set; }
        public string url { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string imageurl { get; set; }
        public DateTime time { get; set; }
    }
}
