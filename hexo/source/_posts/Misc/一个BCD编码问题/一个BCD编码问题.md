---
title: 一个BCD编码问题
date: 2020-09-14 21:35:12
tags:
- BCD
- IoT
categories:
- Misc
---

## 背景

最近在接手某一个物联网项目，其中有一个需求是要获某个倾角传感器数据。该传感器使用RS485串口通讯，说明书写得相对简单，只描述了一个自定义的协议。其中关于读取所有角度数据的命令如下：

{% asset_img "ZCT245RD-LBQ-E2-77-通讯协议.png" "通讯协议-1"  %}

根据前文描述的数据协议格式，这里的角度数据分别是：
```
0x00 20 10     # x轴角度 20.10
0x10 05 20     # y轴角度 -5.20
```

显然，这里的报文使用的都是16进制表示，但是看它的备注，却仿佛是把16进制的数据直接当成文本来解读，然后解析成十进制数，最后除以100得到最终的角度数据。我很好奇，为什么不直接使用浮点数来表示，毕竟占用4个字节相对于占用3个字节显示不是什么大问题。<!--more-->

由于看不明白备注中的`AA AB CC`/`CC CD DD`的关系，我甚至对上述解读方式的正确性产生了怀疑——但经询问，厂家表示上述的解释是对的，并贴出了另一种产品型号说明书中类似的例子：

{% asset_img "ZCT-2.png" "通讯协议-2"  %}

例子本身倒没有什么特别的，但是和之前说明书不同，这份说明书在注释中提及了`BCD`编码——注意这里准确地给出了编码技术关键词。

## BCD 编码

我回想起大一计算机概论课程里有提及这种编码方式，但是已经全然不记得细节了。这里直接引用[某百科对BCD码描述](https://baike.baidu.com/item/BCD%E7%A0%81)如下：
> BCD码（Binary-Coded Decimal‎），用4位二进制数来表示1位十进制数中的0~9这10个数码，是一种二进制的数字编码形式，**用二进制编码的十进制代码**。BCD码这种编码形式利用了**四个位元来储存一个十进制的数码**，使二进制和十进制之间的转换得以快捷的进行。这种编码技巧最常用于会计系统的设计里，因为会计制度经常需要对很长的数字串作准确的计算。**相对于一般的浮点式记数法，采用BCD码，既可保存数值的精确度，又可免去使计算机作浮点运算时所耗费的时间**。

这段描述很好的解释了前文中关于为什么不使用浮点数表示数据的疑问。现在的问题是，如何把这个报文里的角度转成十进制呢？显然，问题的关键是如何把两组角度字节数组分别转成角度数值。由于这两个角度字节数组格式完全一致，我们可以写一个公共函数来完成这个解析工作：

- 第1个字节表示符号: 为根据`0x00`/`0x01`解析成`+`/`-`
- 第2个字节表示整数：高四位表示十位数字，低四位表示个位数字
- 第3个字节表示整数：高四位表示小数点后第一位数字，低四位表示小数点后第二位数字

根据以上描述，我们可以写出如下的转码方法：
```csharp
private float ParseAngle(byte[] bytes)
{
    // 符号
    var s  = bytes[0] switch {
        0x00 =>  1 ,
        0x10 => -1,
        _ => throw new System.Exception($"未知的符号位={bytes[0]}！")
    };

    // 整数部分
    var ih = bytes[1] >> 4;
    var il = bytes[1] & 0x0F;
    var i = 10 * ih + il;

    // 小数部分
    var jh = bytes[2] >> 4;
    var jl = bytes[2] & 0x0F;
    var j = 10 * jh + jl;
    return (i + j / 100.0f) * s;
}

public AnglesData ParseXYAngles(ZCT245RD_LBQ_E2_77_ResponseFrame frame)
{
    // check
    if(frame.x == null || frame.x.Length != 3)
    {
        // ...
    }
    if(frame.y == null || frame.y.Length != 3)
    {
        // ...
    }
    // ... check sum
    // ... check others
    var x= ParseAngle(frame.x);
    var y= ParseAngle(frame.y);
    return new AnglesData { X = x, Y = y};
}
```

最后，我们再把说明书里的例子编成单元测试，确保测试通过：
```csharp
public static IEnumerable<Object[]> Get_ReadAllAngles_Response_Parsing_Args()
{
    var frameBytes = new byte[]{ 0x68, 0x0A, 0x00, 0x84, 0x00,0x20,0x10, 0x10, 0x05, 0x20, 0xF3 };
    yield return new Object[] { 
        frameBytes, 20.1, -5.2
    };
}

[Theory]
[MemberData(nameof(Get_ReadAllAngles_Response_Parsing_Args))]
public void Test_ReadAllAngles_Response_Parsing(byte[] frameBytes ,float expectedX, float expectedY)
{
    var parser = new ZCT245RD_LBQ_E2_77_RequestFrameParser();
    var frame = MarshalHelper.BytesToStruct<ZCT245RD_LBQ_E2_77_ResponseFrame>(frameBytes);
    var (x,y) = parser.ParseXYAngles(frame);

    var delta = 0.00000000000001;
    Assert.True(Math.Abs(x - expectedX ) < delta);
    Assert.True(Math.Abs(y - expectedY) < delta );
}
```