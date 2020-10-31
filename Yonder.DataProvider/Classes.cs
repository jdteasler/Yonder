using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Xml.Serialization;

namespace Yonder.DataProvider
{
    public class Classes
    {
        [Required]
        public int Id { get; set; }
        public string ClassName { get; set; }
        public string Description { get; set; }
        public string TeacherName { get; set; }
        
        public DateTime? EndDate { get; set; }

    }
}
