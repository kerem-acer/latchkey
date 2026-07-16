<!-- Thanks for contributing! Keep PRs focused and small. -->

## Summary

<!-- What does this change do, and why? -->

## Related issues

<!-- e.g. "Closes #123" -->

## Testing

<!-- How did you verify this? Which OS/backend(s)? -->

- [ ] `dotnet build` passes
- [ ] Unit tests pass (`dotnet test --project tests/Latchkey.Tests/Latchkey.Tests.csproj`)
- [ ] Added/updated tests for the change

## Checklist

- [ ] Change is focused and matches the surrounding code style
- [ ] Core `Latchkey` stays zero-dependency, AOT-clean, and trim-clean (no new runtime deps/reflection)
- [ ] Public API changes are documented (XML docs + README if user-facing)
