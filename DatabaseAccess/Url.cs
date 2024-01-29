using System.ComponentModel.DataAnnotations;

namespace DatabaseAccess
{
    public class Url
    {
        [Key]
        public int Id { get; set; }
        public string Address { get; set; }
    }
}