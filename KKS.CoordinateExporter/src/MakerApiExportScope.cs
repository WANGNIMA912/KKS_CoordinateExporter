using System;
using HarmonyLib;
using KKAPI.Maker;

namespace KKS.CoordinateExporter
{
    /// <summary>
    /// 导出期间的「伪 maker 作用域」。
    ///
    /// KKAPI / ESF 的服装卡扩展数据保存链会调用
    /// <see cref="MakerAPI.GetCharacterControl"/> 来定位当前角色；
    /// 在 CharaStudio 里它永远返回 null，于是整条保存链被跳过，
    /// ABMX / DBDE 等数据就写不进卡里。
    ///
    /// 这个类配合下面的 Harmony 前缀补丁：进入作用域时把目标角色放进
    /// <see cref="Current"/>，补丁在作用域内直接返回它，作用域结束（Dispose）
    /// 后恢复原值。只影响导出那一小段时间，不改 InsideMaker 状态。
    /// </summary>
    internal static class MakerApiExportScope
    {
        private static ChaControl _current;

        /// <summary>当前作用域内要被当作「maker 角色」的对象；作用域外为 null。</summary>
        internal static ChaControl Current
        {
            get { return _current; }
            set { _current = value; }
        }

        /// <summary>进入作用域；Dispose 时恢复进入前的值（支持嵌套）。</summary>
        internal static IDisposable Enter(ChaControl chaCtrl)
        {
            return new Scope(chaCtrl);
        }

        private sealed class Scope : IDisposable
        {
            private readonly ChaControl _previous;
            private bool _disposed;

            internal Scope(ChaControl chaCtrl)
            {
                _previous = _current;
                _current = chaCtrl;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _current = _previous;
            }
        }
    }

    [HarmonyPatch(typeof(MakerAPI), nameof(MakerAPI.GetCharacterControl))]
    internal static class MakerApiGetCharacterControlPatch
    {
        // 只有导出作用域内才拦截；其余时候交回原方法。
        private static bool Prefix(ref ChaControl __result)
        {
            var chaCtrl = MakerApiExportScope.Current;
            if (chaCtrl == null) return true;

            __result = chaCtrl;
            return false;
        }
    }
}
