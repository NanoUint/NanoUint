namespace NanoUint.EventSystems;

/// <summary>指针事件接口。Component 实现这些接口以接收鼠标/触控事件。</summary>

/// <summary>指针进入交互区域。</summary>
public interface IPointerEnterHandler { void OnPointerEnter(); }

/// <summary>指针离开交互区域。</summary>
public interface IPointerExitHandler { void OnPointerExit(); }

/// <summary>指针按下。</summary>
public interface IPointerDownHandler { void OnPointerDown(); }

/// <summary>指针抬起。</summary>
public interface IPointerUpHandler { void OnPointerUp(); }

/// <summary>完整点击（按下+抬起在同一元素上）。</summary>
public interface IPointerClickHandler { void OnPointerClick(); }
