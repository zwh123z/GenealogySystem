using System;
using System.Linq;
using System.Windows;
using GenealogySystem.Models;

namespace GenealogySystem
{
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请输入用户名和密码！", "提示");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    db.Database.EnsureCreated();
                    var user = db.Users.FirstOrDefault(u => u.Username == username && u.Password == password);

                    if (user != null)
                    {
                        App.CurrentUser = user; // 登录成功，保存全局用户信息
                        MainWindow main = new MainWindow();
                        main.Show();
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("用户名或密码错误，请重试。", "登录失败");
                    }
                }
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show($"登录失败，详细原因：{message}", "系统错误");
            }
        }

        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (username.Length < 3 || password.Length < 3)
            {
                MessageBox.Show("账号密码长度至少为3位", "注册提示");
                return;
            }

            try
            {
                using (var db = new AppDbContext())
                {
                    db.Database.EnsureCreated();

                    if (db.Users.Any(u => u.Username == username))
                    {
                        MessageBox.Show("该用户名已存在！");
                        return;
                    }

                    // --- [核心业务：新用户自动归类逻辑] ---

                    // 1. 分配新的族谱 ID (在现有最大 ID 基础上递增)
                    int newGenId = 11; // 假设 1-10 是演示数据
                    if (db.Members.Any())
                    {
                        newGenId = db.Members.Max(m => m.GenealogyId) + 1;
                    }

                    // 2. 创建该用户在族谱中的“根成员”身份
                    var rootMember = new Member
                    {
                        Name = username, // 默认成员名同用户名
                        Gender = "男",
                        BirthYear = DateTime.Now.Year - 20, // 默认 20 岁
                        GenealogyId = newGenId,
                        Bio = "本族谱的创建者"
                    };
                    db.Members.Add(rootMember);
                    db.SaveChanges(); // 必须先 Save 一次，获取 rootMember.Id

                    // 3. 创建用户账号并绑定 MemberId
                    var newUser = new User
                    {
                        Username = username,
                        Password = password,
                        LinkedMemberId = rootMember.Id // 关键：账号与成员 ID 绑定
                    };
                    db.Users.Add(newUser);
                    db.SaveChanges();

                    MessageBox.Show($"注册成功！\n系统已为您初始化‘族谱 {newGenId}’。\n您现在是该族谱的始祖。", "恭喜");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"注册失败: {ex.Message}");
            }
        }
    }
}