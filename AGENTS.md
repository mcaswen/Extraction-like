# Project delivery workflow

- Only commit, push, or merge branches when the user explicitly requests that action. Otherwise, keep verified changes local and uncommitted.
- Fetch and inspect the remote before integrating. Preserve other contributors' commits and unrelated local work; do not force-push or bypass branch protection. If safe integration requires a decision, ask the user.
- Discussion, diagnosis, and unselected auditions do not authorize replacing game assets. Keep such previews separate until the user confirms a choice.
- Include required Unity `.meta` files and Git LFS content with asset changes. Verify both the remote commit and large-file upload before claiming that teammates can pull the update.
