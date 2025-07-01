using static EMMS.Models.Enumerators;

namespace EMMS.Utility
{
    public class Notification
    {
        public Notification(string message, NotificationType type)
        {
            Message = message;
            Type = type;
        }

        public string Message { get; set; } 
        public NotificationType Type { get; set; }
    }
}
