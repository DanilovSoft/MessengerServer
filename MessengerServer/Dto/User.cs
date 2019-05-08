using Ninject;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace MessengerServer.Dto
{
    public class User
    {
        public int Id { get; set; }

        [NotMapped]
        public string Name { get; set; }

        [NotMapped]
        public string Email { get; set; }

        [StringLength(60, MinimumLength = 60)]
        public string Password { get; set; }
    }
}
