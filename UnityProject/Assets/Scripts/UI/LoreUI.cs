using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// World / character codex (feedback #8 item 7): a scrollable lore panel reachable from the
    /// main menu.
    /// </summary>
    public class LoreUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _panel;
        private ScrollRect _scroll;
        private Text _body;
        private bool _open;

        public bool IsOpen { get { return _open; } }

        private const string Lore =
"<size=30><color=#e8c87a>世 界 · 感 染 纪 事</color></size>\n\n" +
"阿瑞斯病毒并不是一开始就叫这个名字。它最早出现在北境的一处农业试验站，被登记为「A-7 号样本」。\n" +
"没有人知道它为什么会在三天之内传遍整座城市——直到第一批感染者在街头站起来，人们才意识到，" +
"病毒改造的不只是身体，还有进食的欲望。\n\n" +
"第七天，市政厅下达最后一道广播：封锁全部街区，幸存者自行寻找掩体。那之后，无线电里只剩下杂音。\n\n" +
"<size=30><color=#e8c87a>竞 技 场 · 你 为 什 么 在 这</color></size>\n\n" +
"庇护所的地下埋着一台旧时代的广播塔。它每晚向外发射一次信号，也每晚引来一批感染者。\n" +
"为了守住这里，幸存者把地下的装卸场改成了竞技场：用汽油桶、栅栏和弹链，把潮水一样的感染者挡在门外。\n" +
"每挡住一波，广播塔就能多运转一天。\n\n" +
"<size=30><color=#e8c87a>势 力</color></size>\n\n" +
"<color=#9fd08a>幸存者营地</color>\n庇护所就是营地本身。这里没有军队，只有愿意继续活下去的人，" +
"以及一台随时可能熄火的发电机。\n\n" +
"<color=#d08a6a>掠夺者</color>\n封锁线外游荡的武装团伙。他们不生产，只收割。" +
"竞技场里的物资之所以珍贵，一半是因为感染者，另一半是因为他们。\n\n" +
"<color=#8aa8d0>军方封锁线</color>\n城市外围的高墙。没有人见过墙内的人，但每晚都能听到直升机的回声。\n\n" +
"<size=30><color=#e8c87a>人 物 志</color></size>\n\n" +
"<color=#e8c87a>幸存者（你）</color>\n不记得自己是怎么活下来的。只记得醒来时手里攥着一把砍刀，" +
"和半盒不知道还能不能用的火药。\n\n" +
"<color=#e8c87a>商人</color>\n背着比自己还高的登山包，永远笑呵呵。" +
"没人知道他为什么能在感染者中间自由穿行，也没人敢问他的货是从哪儿来的。\n" +
"他收木头和生铁，卖火药和绷带。缺物资的时候，他会免费塞给你一包——但只此一次。\n\n" +
"<color=#e8c87a>铁匠</color>\n从前的汽车修理厂工人。现在他把发动机零件打成刀，把钢管改成枪管。" +
"他话很少，只在锤子落下的时候哼两句跑调的老歌。\n\n" +
"<size=30><color=#e8c87a>感 染 体</color></size>\n\n" +
"<color=#e8c87a>触手怪</color>\n最常见的感染者。它们会绕过障碍一路贴近，靠近后有明显的前摇——抓紧那半秒。\n\n" +
"<color=#e8c87a>呕吐者</color>\n保持距离的远程单位。吐出的酸液会被墙挡住，所以贴墙绕柱是有效的应对。\n\n" +
"<color=#e8c87a>变异体 BOSS</color>\n体型数倍于普通感染者，脚下泛着红色脉冲。它们不是被病毒改造的——" +
"它们是被病毒<color=#ff8a7a>吃掉了一部分</color>之后，又长回来的东西。\n\n" +
"<size=30><color=#e8c87a>生 存 手 册</color></size>\n\n" +
"· 汽油桶会被任意伤害引爆，把感染者引过去再打，效率最高。\n" +
"· 手榴弹伤害是油桶的七成，但它能扔过墙。\n" +
"· 木栅栏能挡住一群感染者，被啃咬会掉耐久，坏了就没了。\n" +
"· 武器的强化只在背包里生效，改造的模块跨局保留。\n" +
"· 出战前把物资装进背包——胜利后背包里的东西才会回到仓库。\n";

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("LoreCanvas", 36);
            _canvas.transform.SetParent(transform, false);

            var dim = UiFactory.CreateImage(_canvas.transform, "Dim", UiFactory.WhiteSprite, new Color(0f, 0f, 0f, 0.86f));
            UiFactory.SetFullStretch(dim.rectTransform);
            dim.raycastTarget = true;

            _panel = UiFactory.CreateRect(_canvas.transform, "Panel");
            UiFactory.Anchor(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 860f));

            var bg = UiFactory.CreateImage(_panel, "Bg", UiFactory.PanelSprite, Color.white);
            UiFactory.SetFullStretch(bg.rectTransform);
            if (UiFactory.PanelSprite == null) bg.color = new Color(0.07f, 0.075f, 0.08f, 0.99f);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var title = UiFactory.CreateText(_panel, "Title", "世 界 观 · 人 物 志", 40, new Color(0.92f, 0.87f, 0.7f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -24f), new Vector2(800f, 54f));

            var close = UiFactory.CreateButton(_panel, "Close", "X", 24, () => SetOpen(false));
            UiFactory.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(56f, 56f));

            var scrollGo = UiFactory.CreateRect(_panel, "Scroll");
            scrollGo.anchorMin = Vector2.zero;
            scrollGo.anchorMax = Vector2.one;
            scrollGo.pivot = new Vector2(0.5f, 0.5f);
            scrollGo.offsetMin = new Vector2(48f, 46f);
            scrollGo.offsetMax = new Vector2(-48f, -104f);
            _scroll = scrollGo.gameObject.AddComponent<ScrollRect>();

            var viewport = UiFactory.CreateRect(scrollGo, "Viewport");
            UiFactory.SetFullStretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.004f);
            vpImg.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            _body = UiFactory.CreateText(content, "Body", Lore, 20, new Color(0.78f, 0.8f, 0.74f), TextAnchor.UpperLeft);
            _body.rectTransform.anchorMin = new Vector2(0f, 1f);
            _body.rectTransform.anchorMax = new Vector2(1f, 1f);
            _body.rectTransform.pivot = new Vector2(0.5f, 1f);
            _body.rectTransform.anchoredPosition = Vector2.zero;
            _body.rectTransform.sizeDelta = new Vector2(0f, 10f);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            _body.lineSpacing = 1.25f;

            _scroll.viewport = viewport;
            _scroll.content = content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 60f;

            var hint = UiFactory.CreateText(_panel, "Hint", "滚轮或拖拽阅读    Esc 关闭", 17, new Color(0.58f, 0.62f, 0.58f), TextAnchor.LowerRight);
            UiFactory.Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 14f), new Vector2(420f, 24f));

            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (_canvas != null) _canvas.enabled = open;
            if (open) ResizeContent();
        }

        private void ResizeContent()
        {
            if (_body == null) return;
            Canvas.ForceUpdateCanvases();
            float h = Mathf.Max(_body.preferredHeight + 20f, 100f);
            _body.rectTransform.sizeDelta = new Vector2(0f, h);
            if (_scroll != null && _scroll.content != null)
                _scroll.content.sizeDelta = new Vector2(0f, h);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
