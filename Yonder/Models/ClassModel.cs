using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Yonder.Models
{
    public class ClassModel
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(50, ErrorMessage = "Class name cannot be longer than 50 characters.")]
        public string ClassName { get; set; }

        [MaxLength(250, ErrorMessage = "Class description cannot be longer than 250 characters.")]
        public string Description { get; set; }
        [Required]
        [MaxLength(50, ErrorMessage = "Teacher name cannot be longer than 50 characters.")]
        public string TeacherName { get; set; }
        public DateTime EndDate { get; set; }
    }
}
