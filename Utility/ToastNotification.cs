using static EMMS.Models.Enumerators;

namespace EMMS.Utility
{
    public class ToastNotification
    {
        public ToastNotification(string message, NotificationType type)
        {
            Message = message;
            Type = type;
        }

        public string Message { get; set; } 
        public NotificationType Type { get; set; }
    }
}
