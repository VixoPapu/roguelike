using ProjectLike.Phase1;
using ProjectLike.Phase2;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLike.Online
{
    public sealed class CoopDownedView : MonoBehaviour
    {
        PlayerController controller; CoopAllyStatus status; Image fill, shade; Text message; AudioLowPassFilter lowPass; bool wasDowned;
        void Start()
        {
            controller = GetComponent<PlayerController>(); status = GetComponent<CoopAllyStatus>();
            var root = new GameObject("DownedHostView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4200;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var panel = new GameObject("Shade", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(root.transform, false); shade = panel.GetComponent<Image>(); shade.color = new Color(.02f,.08f,.12f,0); shade.rectTransform.anchorMin=Vector2.zero;shade.rectTransform.anchorMax=Vector2.one;shade.rectTransform.sizeDelta=Vector2.zero;
            message = Label(root.transform, "DERRIBADO\nUn aliado debe mantener [E] para revivirte", new Vector2(0,46),25);
            var bar = new GameObject("ReviveBar", typeof(RectTransform), typeof(Image));bar.transform.SetParent(root.transform,false);var bg=bar.GetComponent<Image>();bg.color=new Color(.04f,.05f,.06f,.92f);bg.rectTransform.anchorMin=bg.rectTransform.anchorMax=new Vector2(.5f,.5f);bg.rectTransform.sizeDelta=new Vector2(360,24);
            var value = new GameObject("Fill", typeof(RectTransform), typeof(Image));value.transform.SetParent(bar.transform,false);fill=value.GetComponent<Image>();fill.color=new Color(1f,.78f,.14f);fill.rectTransform.anchorMin=fill.rectTransform.anchorMax=new Vector2(0,.5f);fill.rectTransform.pivot=new Vector2(0,.5f);fill.rectTransform.sizeDelta=Vector2.zero; SetVisible(false);
        }
        Text Label(Transform parent,string value,Vector2 position,int size){var root=new GameObject("DownedText",typeof(RectTransform),typeof(Text));root.transform.SetParent(parent,false);var text=root.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.text=value;text.rectTransform.anchorMin=text.rectTransform.anchorMax=new Vector2(.5f,.5f);text.rectTransform.anchoredPosition=position;text.rectTransform.sizeDelta=new Vector2(700,90);return text;}
        void Update(){bool downed=controller&&controller.IsDead&&CoopSession.IsHost;if(downed!=wasDowned){wasDowned=downed;SetVisible(downed);}if(!downed)return;float progress=status?status.ReviveProgress:0f;fill.rectTransform.sizeDelta=new Vector2(356*progress,18);message.text=progress>0?"REVIVIENDO "+Mathf.RoundToInt(progress*100f)+"%":"DERRIBADO\nUn aliado debe mantener [E] para revivirte";}
        void SetVisible(bool value){if(message)message.gameObject.SetActive(value);if(fill)fill.transform.parent.gameObject.SetActive(value);if(shade)shade.color=new Color(.02f,.08f,.12f,value?.42f:0f);var camera=Camera.main;var follow=camera?camera.GetComponent<CameraFollow>():null;if(follow)follow.SetDownedZoom(value);if(camera){if(!lowPass)lowPass=camera.GetComponent<AudioLowPassFilter>()??camera.gameObject.AddComponent<AudioLowPassFilter>();lowPass.enabled=value;lowPass.cutoffFrequency=650f;lowPass.lowpassResonanceQ=1.15f;}}
    }
}
