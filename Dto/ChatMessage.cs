using System;

namespace Dto
{
    public class ChatMessage
    {
        public Guid MessageId { get; set; }
        public string Text;
        public DateTime CreatedUtcDate { get; set; }
        public bool IsMy { get; set; }
    }
}
