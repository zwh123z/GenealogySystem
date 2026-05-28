using Microsoft.EntityFrameworkCore;
using GenealogySystem.Models;

namespace GenealogySystem
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<GenealogyPermission> GenealogyPermissions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 1. 定义连接字符串
            // 请把 password 改成你在 Workbench 登录时用的真实密码
            string connStr = "server=localhost;database=genealogy_db;user=root;password=zwh2005.;AllowLoadLocalInfile=true";

            // 2. 使用 MySQL 驱动
            optionsBuilder.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
        }
    }
}