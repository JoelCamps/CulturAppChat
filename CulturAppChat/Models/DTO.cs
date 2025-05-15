using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CulturAppChat.Models
{
    public class UserDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
    }

    public class MessageDTO
    {
        public int Id { get; set; }
        public int User_id { get; set; }
        public string Message { get; set; }
        public string Send_datetime { get; set; }
    }
}
