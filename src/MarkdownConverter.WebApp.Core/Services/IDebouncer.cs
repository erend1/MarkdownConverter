namespace MarkdownConverter.WebApp.Core.Services;

public interface IDebouncer
{
    void Debounce(int millisecondsDelay, Func<Task> action);
}
