using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GenealogySystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GenealogySystem
{
    public partial class MainWindow : Window
    {
        private string csvPath = "C:/temp/members.csv";
        private int _currentEditingMemberId = 0;
        private int _selectedGenealogyId = 0; // 当前选中的族谱查看上下文

        public MainWindow()
        {
            InitializeComponent();
            using (var db = new AppDbContext()) { db.Database.EnsureCreated(); }
            InitGenealogySelector(); // [补全]：初始化族谱选择器
            LoadData();
        }

        #region 1. Dashboard 统计与权限过滤
        private void InitGenealogySelector()
        {
            using (var db = new AppDbContext())
            {
                int currentUserId = App.CurrentUser?.Id ?? 0;

                // 从数据库查询当前用户被授权的所有族谱 ID
                var allowedIds = db.GenealogyPermissions
                                   .Where(p => p.UserId == currentUserId)
                                   .Select(p => p.GenealogyId)
                                   .ToList();

                var list = new List<dynamic>();
                foreach (int genId in allowedIds)
                {
                    list.Add(new { Id = genId, Name = $"族谱 {genId:D2}" + (genId == 1 ? " (核心)" : " (协作)") });
                }

                cbGenealogySelector.ItemsSource = list;

                // 默认选择第一个
                if (allowedIds.Count > 0)
                {
                    cbGenealogySelector.SelectedValue = allowedIds[0];
                    _selectedGenealogyId = allowedIds[0];
                }
            }
        }
        // 当族谱选择器变化时触发
        private void cbGenealogySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbGenealogySelector.SelectedValue != null)
            {
                _selectedGenealogyId = (int)cbGenealogySelector.SelectedValue;
                LoadData(); // 重新加载数据
            }
        }

        private void LoadData()
        {
            try
            {
                using (var db = new AppDbContext())
                {
                    // 1. [核心修改] 获取当前登录用户的 ID
                    int currentUserId = App.CurrentUser?.Id ?? 0;

                    // 2. [数据库授权逻辑] 从权限关联表中查询该用户有权访问的所有族谱 ID
                    // 这实现了任务要求中的“受邀访问”逻辑
                    List<int> allowedGenIds = db.GenealogyPermissions
                                                .Where(p => p.UserId == currentUserId)
                                                .Select(p => p.GenealogyId)
                                                .ToList();

                    // [补充逻辑]：如果数据库权限表没配置，但用户自己绑定了身份，默认允许看自家的
                    int? myMemberId = App.CurrentUser?.LinkedMemberId;
                    if (myMemberId.HasValue)
                    {
                        var me = db.Members.Find(myMemberId.Value);
                        if (me != null && !allowedGenIds.Contains(me.GenealogyId))
                        {
                            allowedGenIds.Add(me.GenealogyId);
                        }
                    }

                    // 3. 更新下拉框列表：只显示该用户“被授权”的族谱
                    var selectorList = new List<dynamic>();
                    foreach (int id in allowedGenIds)
                    {
                        // 为不同类型的族谱加上标注，方便演示
                        string tag = (id == 1) ? "(公共)" : (id > 10 ? "(我的)" : "(受邀)");
                        selectorList.Add(new { Id = id, Name = $"族谱 {id:D2} {tag}" });
                    }
                    cbGenealogySelector.ItemsSource = selectorList;

                    // 4. [安全检查]：如果当前没选族谱，或者选了一个没权限的（比如手动改了变量）
                    if (_selectedGenealogyId == 0 || !allowedGenIds.Contains(_selectedGenealogyId))
                    {
                        if (allowedGenIds.Count > 0)
                        {
                            // 自动切换到第一个有权限的族谱
                            _selectedGenealogyId = allowedGenIds[0];
                            cbGenealogySelector.SelectedValue = _selectedGenealogyId;
                        }
                        else
                        {
                            lblTotalMembers.Text = "暂无授权";
                            dgMembers.ItemsSource = null;
                            return;
                        }
                    }

                    // 5. 执行数据查询（仅限当前选中的、且有权限的族谱）
                    var query = db.Members.Where(m => m.GenealogyId == _selectedGenealogyId);

                    // 6. 正常的统计与列表逻辑
                    lblTotalMembers.Text = query.LongCount().ToString();
                    int male = query.Count(m => m.Gender == "男");
                    int female = query.Count(m => m.Gender == "女");
                    lblGenderRatio.Text = $"{male} : {female}";

                    // 列表展示
                    dgMembers.ItemsSource = query.OrderBy(m => m.Id).Take(200).ToList();

                    // 更新侧边栏欢迎词
                    if (App.CurrentUser != null)
                        lblWelcome.Text = $"欢迎，{App.CurrentUser.Username}！";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("数据加载异常: " + ex.Message);
            }
        }
        #endregion

        #region 2. 成员管理 CRUD
        private void txtSearchId_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = txtSearchId.Text.Trim();
            if (string.IsNullOrEmpty(input)) { LoadData(); return; }
            if (int.TryParse(input, out int sid))
            {
                using var db = new AppDbContext();
                // 仅搜索当前族谱下的 ID
                dgMembers.ItemsSource = db.Members.Where(m => m.Id == sid && m.GenealogyId == _selectedGenealogyId).ToList();
            }
        }

        private void txtSearchName_TextChanged(object sender, TextChangedEventArgs e)
        {
            string key = txtSearchName.Text.Trim();
            if (string.IsNullOrEmpty(key)) { LoadData(); return; }
            using var db = new AppDbContext();
            // 仅搜索当前族谱下的姓名
            dgMembers.ItemsSource = db.Members.Where(m => m.Name.Contains(key) && m.GenealogyId == _selectedGenealogyId).Take(100).ToList();
        }

        private void dgMembers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgMembers.SelectedItem is Member selected)
            {
                _currentEditingMemberId = selected.Id;
                edtName.Text = selected.Name;
                foreach (ComboBoxItem item in edtGender.Items)
                {
                    if (item.Content.ToString() == selected.Gender) { edtGender.SelectedItem = item; break; }
                }
                edtBirthYear.Text = selected.BirthYear.ToString();
                edtFatherId.Text = selected.FatherId?.ToString() ?? "";
                edtMotherId.Text = selected.MotherId?.ToString() ?? "";
                edtBio.Text = selected.Bio;
                btnSaveMember.Content = "💾 保存修改 (ID: " + selected.Id + ")";
            }
        }

        private void btnAddNew_Click(object sender, RoutedEventArgs e)
        {
            _currentEditingMemberId = 0;
            edtName.Text = ""; edtBirthYear.Text = "2000"; edtFatherId.Text = ""; edtMotherId.Text = ""; edtBio.Text = "";
            btnSaveMember.Content = "💾 确认新增";
        }

        private void btnSaveMember_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(edtName.Text)) return;
            using (var db = new AppDbContext())
            {
                try
                {
                    Member m = (_currentEditingMemberId == 0) ? new Member() : db.Members.Find(_currentEditingMemberId);
                    m.Name = edtName.Text;
                    m.Gender = (edtGender.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "男";
                    m.BirthYear = int.Parse(edtBirthYear.Text);
                    m.Bio = edtBio.Text;
                    m.GenealogyId = _selectedGenealogyId; // 新增/修改的成员归属于当前选中的族谱
                    if (int.TryParse(edtFatherId.Text, out int fid)) m.FatherId = fid; else m.FatherId = null;
                    if (int.TryParse(edtMotherId.Text, out int mid)) m.MotherId = mid; else m.MotherId = null;
                    if (_currentEditingMemberId == 0) db.Members.Add(m);
                    db.SaveChanges();
                    LoadData();
                    MessageBox.Show("保存成功！");
                }
                catch (Exception ex) { MessageBox.Show("失败: " + ex.Message); }
            }
        }

        private void btnDeleteMember_Click(object sender, RoutedEventArgs e)
        {
            if (dgMembers.SelectedItem is Member selected)
            {
                if (MessageBox.Show($"确定删除 [{selected.Name}] 吗？", "警告", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using var db = new AppDbContext();
                    var m = db.Members.Find(selected.Id);
                    if (m != null) { db.Members.Remove(m); db.SaveChanges(); LoadData(); }
                }
            }
        }

        // [任务要求 4] 族谱邀请功能：将当前族谱共享给其他用户
        private void btnInvite_Click(object sender, RoutedEventArgs e)
        {
            // 1. 检查当前是否选中了族谱
            if (_selectedGenealogyId == 0)
            {
                MessageBox.Show("请先在左侧选择一个要分享的族谱！");
                return;
            }

            // 2. 调用刚才写的自定义对话框
            string targetUsername = SimpleInputDialog.Show("邀请协作", $"请输入被邀请人的用户名：\n(该用户将获得族谱 {_selectedGenealogyId:D2} 的查看权限)");

            if (string.IsNullOrWhiteSpace(targetUsername)) return;

            using (var db = new AppDbContext())
            {
                try
                {
                    // 3. 查用户
                    var targetUser = db.Users.FirstOrDefault(u => u.Username == targetUsername);
                    if (targetUser == null)
                    {
                        MessageBox.Show($"未找到用户：{targetUsername}");
                        return;
                    }

                    // 4. 检查是否重复授权
                    if (db.GenealogyPermissions.Any(p => p.UserId == targetUser.Id && p.GenealogyId == _selectedGenealogyId))
                    {
                        MessageBox.Show("该用户已经在协作名单中。");
                        return;
                    }

                    // 5. 插入权限记录
                    db.GenealogyPermissions.Add(new GenealogyPermission
                    {
                        UserId = targetUser.Id,
                        GenealogyId = _selectedGenealogyId
                    });
                    db.SaveChanges();

                    MessageBox.Show($"邀请成功！用户 '{targetUsername}' 登录后即可看到该族谱。");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("邀请失败：" + ex.Message);
                }
            }
        }
        #endregion

        #region 3. 家族树图形化展示 (祖先查询也在此处显示)
        // [修正]：将 BuildDefaultTree() 从原来的 Tab 2 转移到这里并修正
        private void BuildDefaultTree()
        {
            tvFamilyTree.Items.Clear();
            using (var db = new AppDbContext())
            {
                // 仅加载当前选定族谱的前 300 人进行树形展示
                var list = db.Members.Where(m => m.GenealogyId == _selectedGenealogyId).Take(300).ToList();
                var roots = list.Where(m => (m.FatherId ?? 0) == 0 && (m.MotherId ?? 0) == 0).ToList();
                foreach (var r in roots) tvFamilyTree.Items.Add(CreateRecursiveItem(r, list));
            }
        }

        private TreeViewItem CreateRecursiveItem(Member m, List<Member> all)
        {
            var item = new TreeViewItem { Header = $"{m.Name} (ID:{m.Id})", IsExpanded = m.Id < 50 }; // 默认展开部分节点
            var children = all.Where(c => c.FatherId == m.Id || c.MotherId == m.Id).ToList(); // 包含母亲ID的孩子
            foreach (var child in children) item.Items.Add(CreateRecursiveItem(child, all));
            return item;
        }

        private void btnShowAncestors_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtAncestorId.Text, out int id)) return;
            tvFamilyTree.Items.Clear();
            using var db = new AppDbContext();
            var current = db.Members.Find(id);
            var ancestors = new List<Member>();
            while (current != null)
            {
                ancestors.Add(current);
                // 同时追溯 FatherId 和 MotherId
                current = db.Members.Find(current.FatherId ?? current.MotherId);
            }
            ancestors.Reverse();
            TreeViewItem root = null; TreeViewItem cur = null;
            foreach (var a in ancestors)
            {
                var item = new TreeViewItem { Header = $"⬆️ {a.Name} ({a.BirthYear})", IsExpanded = true };
                if (root == null) root = item; else cur.Items.Add(item);
                cur = item;
            }
            if (root != null) tvFamilyTree.Items.Add(root);
            MainTabs.SelectedIndex = 2; // 跳转到树形页
        }
        #endregion

        #region 4. 亲缘通路 BFS 搜索
        private void btnFindPath_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtPathId1.Text, out int id1) || !int.TryParse(txtPathId2.Text, out int id2)) return;
            using var db = new AppDbContext();
            // 仅在当前族谱内搜索
            var all = db.Members.Where(m => m.GenealogyId == _selectedGenealogyId).Take(10000).ToList();
            var q = new Queue<List<Member>>(); var visited = new HashSet<int> { id1 };
            var start = all.FirstOrDefault(x => x.Id == id1); if (start == null) return;
            q.Enqueue(new List<Member> { start });
            while (q.Count > 0)
            {
                var p = q.Dequeue(); var last = p.Last();
                if (last.Id == id2) { MessageBox.Show("通路: " + string.Join(" ➔ ", p.Select(x => x.Name))); return; }
                // 邻居同时包括父 ID 和 母 ID
                var neighbors = all.Where(m => m.Id == last.FatherId || m.Id == last.MotherId || m.FatherId == last.Id || m.MotherId == last.Id).ToList();
                foreach (var n in neighbors) if (!visited.Contains(n.Id)) { visited.Add(n.Id); var np = new List<Member>(p) { n }; q.Enqueue(np); }
            }
            MessageBox.Show("未找到通路");
        }
        #endregion

        #region 5. 任务三：数据工程 (CSV、极速导入、备份)
        // [修复] 生成 CSV 时的 allMembers 变量未定义问题
        private void btnGenerateCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory("C:/temp");
                using (StreamWriter sw = new StreamWriter(csvPath, false, Encoding.UTF8))
                {
                    sw.WriteLine("Id,Name,Gender,BirthYear,Bio,GenealogyId,FatherId,MotherId");
                    Random rnd = new Random();
                    int id = 1;
                    List<Member> memPool = new List<Member>(); // [修正]：将 memPool 定义在方法内部
                    string[] surs = { "张", "王", "李", "赵", "刘", "陈" };
                    string[] mNs = { "伟", "强", "勇", "明" }; string[] fNs = { "芳", "秀", "静", "丽" };

                    string GetRandomName(string gender)
                    { // 辅助函数
                        string sur = surs[rnd.Next(surs.Length)];
                        string n1 = (gender == "男") ? mNs[rnd.Next(mNs.Length)] : fNs[rnd.Next(fNs.Length)];
                        return sur + n1 + ((rnd.Next(2) == 0) ? "" : n1);
                    }

                    // 生成大族谱骨干 (32代)
                    int curYear = 1000;
                    for (int gen = 1; gen <= 32; gen++)
                    {
                        string g = (rnd.Next(2) == 0) ? "男" : "女";
                        string name = GetRandomName(g) + $"(第{gen:D2}代)";
                        string f = "", m = "";
                        if (gen > 1)
                        {
                            var lastParent = memPool.Last();
                            if (lastParent.Gender == "男") f = lastParent.Id.ToString();
                            else m = lastParent.Id.ToString();
                            curYear += rnd.Next(25, 35);
                        }
                        sw.WriteLine($"{id},{name},{g},{curYear},核心传承,1,{f},{m}");
                        memPool.Add(new Member { Id = id, Gender = g, BirthYear = curYear });
                        id++;
                    }
                    // 填满大族谱至 55000
                    while (id <= 55000)
                    {
                        string g = (rnd.Next(2) == 0) ? "男" : "女";
                        var parent = memPool[rnd.Next(memPool.Count)];
                        int bY = Math.Min(2024, parent.BirthYear + rnd.Next(20, 45));
                        string f = (parent.Gender == "男") ? parent.Id.ToString() : "";
                        string m = (parent.Gender == "女") ? parent.Id.ToString() : "";
                        sw.WriteLine($"{id},{GetRandomName(g)},{g},{bY},分支,1,{f},{m}");
                        memPool.Add(new Member { Id = id, Gender = g, BirthYear = bY }); // [修正]：这里也要加入 memPool
                        id++;
                    }
                    // 补齐 100001
                    while (id <= 100001)
                    {
                        string g = (rnd.Next(2) == 0) ? "男" : "女";
                        // 随机挂载到一个已有的成员下，确保亲缘关系 (简化处理，挂到族谱 1 的骨干上)
                        var parent = memPool[rnd.Next(memPool.Count)];
                        int bY = Math.Min(2024, parent.BirthYear + rnd.Next(20, 45));
                        string f = (parent.Gender == "男") ? parent.Id.ToString() : "";
                        string m = (parent.Gender == "女") ? parent.Id.ToString() : "";
                        sw.WriteLine($"{id},{GetRandomName(g)},{g},{bY},散户,{rnd.Next(2, 11)},{f},{m}"); id++;
                    }
                }
                MessageBox.Show("CSV 生成成功！保存在 C:/temp/members.csv");
            }
            catch (Exception ex) { MessageBox.Show("失败: " + ex.Message); }
        }

        private void btnFastImport_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(csvPath))
            {
                MessageBox.Show("请先点击生成 CSV 文件！");
                return;
            }

            // 改变鼠标指针，提示用户程序正在工作，请勿关闭
            this.Cursor = System.Windows.Input.Cursors.Wait;

            using (var db = new AppDbContext())
            {
                try
                {
                    // --- 核心补丁：将 SQL 执行超时时间设为 600 秒 (10分钟) ---
                    db.Database.SetCommandTimeout(600);

                    db.Database.EnsureCreated();

                    // 演示前清空旧数据
                    db.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 0; TRUNCATE TABLE Members; SET FOREIGN_KEY_CHECKS = 1;");

                    MessageBox.Show("开始导入 10 万条数据，期间界面可能会暂时失去响应，请耐心等待弹窗提示...");

                    // 1. 同步读取所有行
                    var lines = File.ReadAllLines(csvPath);
                    var dataLines = lines.Skip(1).ToList();

                    Stopwatch sw = Stopwatch.StartNew();

                    // 2. 分批处理（每 5000 行拼成一个大 SQL）
                    int batchSize = 5000;
                    for (int i = 0; i < dataLines.Count; i += batchSize)
                    {
                        var batch = dataLines.Skip(i).Take(batchSize).ToList();

                        StringBuilder sqlBuilder = new StringBuilder();
                        sqlBuilder.Append("INSERT INTO Members (Id, Name, Gender, BirthYear, Bio, GenealogyId, FatherId, MotherId) VALUES ");

                        List<string> rows = new List<string>();
                        foreach (var line in batch)
                        {
                            var p = line.Split(',');
                            // 处理可能存在的空字符串 ID
                            string f = (string.IsNullOrEmpty(p[6]) ? "NULL" : p[6]);
                            string m = (string.IsNullOrEmpty(p[7]) ? "NULL" : p[7]);

                            rows.Add($"({p[0]}, '{p[1]}', '{p[2]}', {p[3]}, '{p[4]}', {p[5]}, {f}, {m})");
                        }

                        sqlBuilder.Append(string.Join(",", rows));
                        sqlBuilder.Append(";");

                        // 3. 执行大批量 SQL 插入
                        db.Database.ExecuteSqlRaw(sqlBuilder.ToString());
                    }

                    sw.Stop();
                    MessageBox.Show($"任务三演示成功！\n\n总记录数: {db.Members.Count()}\n总耗时: {sw.ElapsedMilliseconds / 1000.0:F2} 秒", "导入完成");

                    LoadData(); // 刷新主界面 Dashboard
                }
                catch (Exception ex)
                {
                    // 捕获并显示具体的底层错误
                    string errorDetail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    MessageBox.Show("导入失败，详细原因：\n" + errorDetail);
                }
                finally
                {
                    // 恢复鼠标指针
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                }
            }
        }
        private async void btnExportBranch_Click(object sender, RoutedEventArgs e)
        {
            // 1. 验证输入
            if (!int.TryParse(txtExportId.Text, out int rid))
            {
                MessageBox.Show("请输入有效的成员 ID");
                return;
            }

            // 2. 界面反馈：禁用按钮防止重复操作，鼠标变忙碌状态
            btnExportBranch.IsEnabled = false; // 刚才报错就是因为 XAML 没写 x:Name
            this.Cursor = System.Windows.Input.Cursors.Wait;

            try
            {
                // 3. 开启后台任务，把 5 万人的运算丢给 CPU 后台处理，界面就不会卡了
                await Task.Run(() =>
                {
                    using (var db = new AppDbContext())
                    {
                        // 设置长超时，防止大数据量查询中断
                        db.Database.SetCommandTimeout(300);

                        // MySQL 8.0 递归查询语句
                        string sql = "WITH RECURSIVE B AS (SELECT * FROM Members WHERE Id={0} UNION ALL SELECT m.* FROM Members m JOIN B ON m.FatherId=B.Id OR m.MotherId=B.Id) SELECT * FROM B;";

                        // 执行查询。AsNoTracking 提高性能，减少内存开销
                        var list = db.Members.FromSqlRaw(sql, rid).AsNoTracking().ToList();

                        if (list.Count == 0)
                        {
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show("未找到该分支数据。"));
                            return;
                        }

                        // 4. 准备保存路径
                        string folderPath = @"C:\temp\backups";
                        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                        string outPath = Path.Combine(folderPath, $"branch_backup_{rid}.csv");

                        // 5. 流式写入文件 (直接写入磁盘，不占用大量内存)
                        using (StreamWriter writer = new StreamWriter(outPath, false, Encoding.UTF8))
                        {
                            writer.WriteLine("Id,Name,Gender,BirthYear,Bio,GenealogyId,FatherId,MotherId");
                            foreach (var m in list)
                            {
                                writer.WriteLine($"{m.Id},{m.Name},{m.Gender},{m.BirthYear},{m.Bio},{m.GenealogyId},{m.FatherId},{m.MotherId}");
                            }
                        }

                        // 6. 回到主线程弹出成功提示
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"任务三：分支导出成功！\n\n导出人数：{list.Count}\n已保存至：{outPath}", "操作完成");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败，原因：" + ex.Message);
            }
            finally
            {
                // 7. 无论成功失败，恢复按钮状态和鼠标指针
                btnExportBranch.IsEnabled = true;
                this.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        #endregion

        #region 6. 导航逻辑
        private void Nav_Dashboard_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 0;
        private void Nav_Members_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 1;
        private void Nav_Tree_Click(object sender, RoutedEventArgs e) { MainTabs.SelectedIndex = 2; BuildDefaultTree(); }
        private void Nav_Query_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 3;
        private void Nav_Data_Click(object sender, RoutedEventArgs e) => MainTabs.SelectedIndex = 4;
        private void btnRefresh_Click(object sender, RoutedEventArgs e) => LoadData();
        private void btnExit_Click(object sender, RoutedEventArgs e) { new Login().Show(); this.Close(); }
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) { if (txtSearchName.Text == "搜索...") txtSearchName.Text = ""; }
        #endregion
    }
    // 这是一个简单的输入对话框工具类，不需要额外引用
    public static class SimpleInputDialog
    {
        public static string Show(string title, string prompt)
        {
            Window window = new Window
            {
                Title = title,
                Width = 350,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            StackPanel stackPanel = new StackPanel { Margin = new Thickness(20) };
            TextBlock textBlock = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) };
            TextBox textBox = new TextBox { Height = 25, VerticalContentAlignment = VerticalAlignment.Center };
            Button button = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 15, 0, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)),
                Foreground = System.Windows.Media.Brushes.White,
                IsDefault = true
            };

            button.Click += (s, e) => { window.DialogResult = true; window.Close(); };

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(button);
            window.Content = stackPanel;

            if (window.ShowDialog() == true) return textBox.Text;
            return null;
        }
    }
}
