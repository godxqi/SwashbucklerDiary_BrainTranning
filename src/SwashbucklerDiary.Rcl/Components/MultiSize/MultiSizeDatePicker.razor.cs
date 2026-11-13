using Microsoft.AspNetCore.Components;
using OneOf;

namespace SwashbucklerDiary.Rcl.Components
{
    public partial class MultiSizeDatePicker : DialogComponentBase
    {
        private DateOnly mobileMin;

        private DateOnly mobileMax;

        private DateOnly? desktopMin;

        private DateOnly? desktopMax;

        [Parameter]
        public DateOnly Value { get; set; }

        [Parameter]
        public EventCallback<DateOnly> ValueChanged { get; set; }

        [Parameter]
        public DateOnly? Min { get; set; }

        [Parameter]
        public DateOnly? Max { get; set; }

        /// <summary>
        /// 要标点的日子（比如「这天写过日课」）。<b>只有桌面端有效</b>——
        /// 手机端那个 <c>PMobileDatePicker</c> 根本没有 <c>Events</c> 参数
        /// （整个 <c>Masa.Blazor.MobileComponents</c> 程序集里连这个字符串都找不到），
        /// 所以手机端要靠调用方自己给一句「这天还没写过」之类的提示补上。
        /// </summary>
        [Parameter]
        public OneOf<DateOnly[], Func<DateOnly, bool>>? Events { get; set; }

        /// <summary>标点的颜色。<b>同样只有桌面端有效</b>，理由见 <see cref="Events"/>。</summary>
        [Parameter]
        public OneOf<string, Func<DateOnly, string>, Func<DateOnly, string[]>>? EventColor { get; set; }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            mobileMin = (Min is null || Min == default) ? DateOnly.FromDateTime(DateTime.Now.AddYears(-100)) : Min.Value;
            desktopMin = Min == default(DateOnly) ? null : Min;
            mobileMax = (Max is null || Max == default) ? DateOnly.FromDateTime(DateTime.Now.AddYears(100)) : Max.Value;
            desktopMax = Max == default(DateOnly) ? null : Max;
        }

        private async Task UpdateDisplay(bool value)
        {
            if (Visible)
            {
                await InternalVisibleChanged(false);
            }
        }

        private async Task InternalValueChanged(DateOnly value)
        {
            Value = value;

            if (ValueChanged.HasDelegate)
            {
                await ValueChanged.InvokeAsync(value);
            }
        }
    }
}
