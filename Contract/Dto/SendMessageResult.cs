﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Contract.Dto
{
    public class SendMessageResult
    {
        public byte[] MessageId { get; set; }
        public DateTime Date { get; set; }
    }
}
