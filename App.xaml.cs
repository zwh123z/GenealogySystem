using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GenealogySystem.Models; 
namespace GenealogySystem
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // 全局静态变量，存储当前登录的用户信息
        public static User CurrentUser { get; set; }
    }
}
