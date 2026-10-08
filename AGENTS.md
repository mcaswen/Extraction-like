# Project delivery workflow

- The user has requested that confirmed project changes be committed, pushed, and included in the original repository's `dev` branch after appropriate verification. If developing on a work branch, merge it into `dev` before reporting delivery complete.
- Fetch and inspect the remote before integrating. Preserve other contributors' commits and unrelated local work; do not force-push or bypass branch protection. If safe integration requires a decision, ask the user.
- Discussion, diagnosis, and unselected auditions do not authorize replacing game assets. Keep such previews separate until the user confirms a choice.
- Include required Unity `.meta` files and Git LFS content with asset changes. Verify both the remote commit and large-file upload before claiming that teammates can pull the update.
