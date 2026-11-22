using System;
using System.Collections.Generic;
using System.Text;

namespace HY.ApiService.Controllers.Requests
{
    public class RegisterRequest
    {
        public string Nickname { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
    }
}
