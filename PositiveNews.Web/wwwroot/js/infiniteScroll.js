// Watches a sentinel element and invokes a .NET method when it scrolls into view.
// Used by Home.razor.cs to load more stories as the user scrolls down.
export function observe(dotNetRef, methodName, sentinel) {
    const observer = new IntersectionObserver((entries) => {
        if (entries.some(e => e.isIntersecting)) {
            dotNetRef.invokeMethodAsync(methodName);
        }
    }, { rootMargin: '300px' });

    observer.observe(sentinel);
    return observer;
}
