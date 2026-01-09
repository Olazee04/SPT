using Microsoft.AspNetCore.Identity;

namespace SPT.Models
{
    public class ApplicationUser : IdentityUser
    {
        public Student? Student { get; set; }
        public Mentor? Mentor { get; set; }
       
    }
}
