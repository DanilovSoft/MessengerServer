using System;
using System.Collections.Generic;
using System.Text;

namespace Contract.Dto
{
    public class ChatMessage
    {
        public string Text;

        public DateTime CreatedUtcDate { get; set; }
        
        public bool IsMy { get; set; }
    }
}
