using System.ComponentModel.DataAnnotations;

namespace MessengerServer.Dto
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }

        [StringLength(60, MinimumLength = 60)]
        public string Password { get; set; }
    }
}
