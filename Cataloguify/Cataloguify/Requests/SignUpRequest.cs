using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cataloguify.Requests
{
    public class SignUpRequest
    {   
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public SignUpRequest(string email, string username, string password) 
        {
            Email = email;
            Username = username;
            Password = password;
        }
    }
}
