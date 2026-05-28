using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenealogySystem.Models
{
    public class Member
    {
        public int Id { get; set; }
        public int GenealogyId { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; } // "男" 或 "女"
        public int BirthYear { get; set; }
        public int? DeathYear { get; set; }
        public string? Bio { get; set; }//生平简介

        // 关系 ID (允许为空)
        public int? FatherId { get; set; }
        public int? MotherId { get; set; }
        public int? SpouseId { get; set; }

        // 逻辑属性：用于在 TreeView 中自动递归显示子孙
        public List<Member> Children { get; set; } = new List<Member>();
    }
}
