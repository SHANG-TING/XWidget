﻿using System;
using System.Collections.Generic;
using System.Text;

namespace XWidget.Web.Mvc.JsonMask {
    /// <summary>
    /// 遮罩方法
    /// </summary>
    public enum MaskMethod {
        /// <summary>
        /// 控制器
        /// </summary>
        Controller,

        /// <summary>
        /// 動作、方法名稱
        /// </summary>
        ActionName,

        /// <summary>
        /// 模式名稱
        /// </summary>
        PatternName,

        /// <summary>
        /// 輸出類型
        /// </summary>
        ReturnType,

        /// <summary>
        /// 包裝類型
        /// </summary>
        PackageType,

        /// <summary>
        /// 定義類型
        /// </summary>
        DeclaringType
    }
}
