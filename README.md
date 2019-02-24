# flyfire.CustomSerialPort

一个增强的自定义串口类，实现协议无关的数据帧完整接收功能，支持跨平台使用


## 使用SerialPortStream基础类库

https://github.com/jcurl/serialportstream


## 介绍文档

https://www.cnblogs.com/flyfire-cn/p/10356991.html



采用接收超时机制，两次通讯之间需有一定的间隔时间

如间隔时间太短，则需要拆包(默认128ms,可配置)

Author:赫山老妖（flyfire.cn）

https://www.cnblogs.com/flyfire-cn

