﻿
using System;

namespace Contract.Dto
{
    public class ChatUser
    {
        public int UserId;
        public Uri AvatarUrl { get; set; }
        public string Name { get; set; }
        public long ChatId { get; set; }
        public ChatMessage LastMessage { get; set; }
    }
}