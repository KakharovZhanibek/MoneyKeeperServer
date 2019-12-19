using System;
using System.Collections.Generic;
using System.Text;

namespace MoneyKeeper.Models
{
    public class ApplicationUser
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

        public decimal Balance { get; set; }
    }
}
