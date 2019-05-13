using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace MessengerServer.Dto
{
    public class ChatList
    {
        [Key]
        [ForeignKey(nameof(User))]
        public int UserId { get; set; }
        public User User { get; set; }

        public virtual ICollection<User> Chats { get; set; }
    }
}
