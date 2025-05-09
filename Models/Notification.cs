namespace EMMS.Models
{
    public class Notification
    {
        public int id { get; set; }
        public string type { get; set; }
        public string icon { get; set; }
        public string message { get; set; }
        public string time { get; set; }
        public string controller { get; set; }
        public string action { get; set; }
        public string seen { get; set; } = "false";
    }
}
