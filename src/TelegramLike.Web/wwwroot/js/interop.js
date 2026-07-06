// Minimal JS interop for the Web BFF. Kept intentionally tiny: one helper so the
// Login page can hand the session token to /auth/signin as a real POST form body
// instead of a query-string parameter (see Login.razor / Program.cs "/auth/signin").
window.telegramLike = window.telegramLike || {};

window.telegramLike.submitForm = (formElement) => {
    if (formElement) formElement.submit();
};
