# Jmeter自动化监控
环境:Windows 7以上，.NET 4.0以上

运行jmeter脚本后入mysql数据库。需要用db.sql先建表。请用Visual Studio 2010以上编译项目后生成exe文件。
使用前注意配置app.config中的配置:
数据库连接字符串请修改为自身环境的数据库连接字符串

    <add name="ApplicationServices" connectionString="Data Source=10.24.177.135;Initial Catalog=aspnetdb;Persist Security Info=True;User ID=sa;Password=1qaz" providerName="System.Data.SqlClient" />
    
以下内容也需要修改

      <!--要运行的Jmeter .bat文件-->
      
    <add key="JMeterCmdFile" value="D:\apache-jmeter-2.9\bin\jmeter.bat" />
    
    <!--用例名-->
    
    <add key="AppName" value="AllenTest" />
    
    <!--回归用例.jmx快捷方式存放的文件夹。文件夹内可以放多个.jmx文件-->
    
    <add key="JMeterInputFileFolder" value="C:\temp\new" />
    
    <!--Jmeter输出文件目录。可随意设置为本地机器上的文件夹-->
    
    <add key="JmeterOutputFolder" value="C:\temp" />
    
    
修改完成后直接运行exe即可执行jmeter脚本并将结果入库。
    
后续可以配合告警模块，日报模块，页面模块等实现完整的监控系统。
    
 
注意:jmeter必须增加一个察看结果树，并且configure需要全部勾选，另外日志部分需要设置为${__P(JmeterAuto_LogFile)}
