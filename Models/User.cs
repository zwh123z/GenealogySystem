using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenealogySystem.Models
{
    public class User
    {
           
            public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;        // 新增：关联的族谱成员 ID
        public int? LinkedMemberId { get; set; }


    }
}

