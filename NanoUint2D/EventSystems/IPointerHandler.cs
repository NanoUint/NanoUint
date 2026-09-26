namespace NanoUint.EventSystems;

/// <summary>Pointer event interfaces for mouse/touch input.</summary>

/// <summary>Pointer entered the interactive area.</summary>
public interface IPointerEnterHandler { void OnPointerEnter(); }

/// <summary>Pointer exited the interactive area.</summary>
public interface IPointerExitHandler { void OnPointerExit(); }

/// <summary>Pointer pressed down.</summary>
public interface IPointerDownHandler { void OnPointerDown(); }

/// <summary>Pointer released.</summary>
public interface IPointerUpHandler { void OnPointerUp(); }

/// <summary>Full click (press and release on the same element).</summary>
public interface IPointerClickHandler { void OnPointerClick(); }
