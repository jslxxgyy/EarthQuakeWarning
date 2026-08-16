# EarthQuakeWarning

基于 C# / WPF 的地震预警软件

**本软件仅供学习交流使用，请勿用于生产环境，下载后请在 24 小时内删除**

**请勿传播或进行二次分享**

如果此软件侵犯了您的权益，请发 Issue 或 联系我 [jslxxgyy@outlook.com](mailto:jslxxgyy@outlook.com)  
作者将会尽快处理  

## 免责声明  

由于部分常量与官方稍有出入，可能导致部分数据有偏差，请以官方为准。  
请勿完全依赖此软件，由于依赖此软件造成的问题作者概不负责。  
由于此软件造成的任何人力物力财力等损失与作者无关  
！！！请使用官方 [地震预警](https://download.chinaeew.cn/mobile) APP！！！  

## 与前作的区别

此软件由 Kengwang 的地震预警软件分支而来，在前作的基础上进行了全面更新，主要改变：
* 无边框全屏的预警窗口
* 触摸屏操作适配
* 移除复杂的 GNSS 定位，直接使用 Windows 位置 API
* 不再使用 Windows 自带的语音合成，直接使用官方地震预警警报音
* 自定义警报持续时间
* 即时生效的任务栏托盘图标开关
如在使用此分支项目的过程中遇到问题,请各位不要再去骚扰原作者!

## 可靠性

软件的开发版成功在2026年8月4日23时58分与2026年7月13日05时02分的四川珙县地震中发出了预警信号，具有一定的可靠性

## 最低系统要求

Windows 10 1809 及以上，或 Windows 10 LTSC 2019 及以上，内核版本 10.0.17763 及以上  
支持 x64 与 x86 架构 CPU；ARM64 设备请使用专门发布的 `win-arm64` 版本以获得原生运行性能，
否则默认版本在 Windows 11 ARM 上会以 x64 仿真方式运行  
需要安装 Microsoft Windows .NET Desktop Runtime 10.0

## AI 生成说明

部分修改使用 DeepSeek-v4-flash-0731 完成，并使用相同模型对原先的代码添加了一些注释，开发者对 AI 生成代码内容均实行了检查以确保其可靠性

## 使用的三方库与开源项目

* lepoco/wpfui: [https://github.com/lepoco/wpfui](https://github.com/lepoco/wpfui)
* Microsoft.Extensions.DependencyInjection
* serilog/serilog: [https://github.com/serilog/serilog](https://github.com/serilog/serilog)
* 腾讯地图拾取系统: [https://lbs.qq.com/getPoint/](https://lbs.qq.com/getPoint/)
* Vanara.PInvoke.Kernel32: [https://github.com/dahall/Vanara](https://github.com/dahall/Vanara)
* NAudio: [https://github.com/naudio/NAudio](https://github.com/naudio/NAudio)
* GuerrillaNtp: [https://github.com/robertvazan/guerrillantp](https://github.com/robertvazan/guerrillantp)

## 使用的 API 源

* [成都高新减灾研究所](http://www.365icl.com/) / [成都市美幻科技有限公司](http://www.huania.com/) 的 [地震预警](https://download.chinaeew.cn/mobile)
* [四川地震局](https://www.scdzj.gov.cn/)
* [Wolfx 的 减灾 API](https://api.wolfx.jp/)

## 许可

本软件基于 [GPL-v3](LICENCE) 协议授权

```
    EarthQuakeWarning.App
    Copyright (C) 2023  Kengwang
    Copyright (C) 2026  jslxxgyy

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
```

