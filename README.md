# SmartQQ-use-Automation
Receiving and sending Tencent QQ messages automatically by using Microsoft Automation technology
借助微软自动化测试获取QQ窗口文本消息元素，然后爬取聊天消息内容，传到服务器的消息自动回复接口，获取回复消息（HTTP GET method）可以回复文本，图片，文件，表情包。主要是借助win32api windows剪贴板等工具包。语料库存在服务器数据库中，自动回复API使用python，可以进行分词，情感分析，查询数据库，然后回复给C#前台
程序可以同时检测多个聊天窗口，超时没有消息的窗口会自动关闭，依然存在的窗口不会重复查找元素。
