#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
    echo "[错误] 当前目录不在 Git 仓库中，或 Git 未正确安装"
    echo "[提示] 请将以上完整输出截图或复制，联系程序组处理"
    exit 1
fi

cd "$repo_root"

current_branch="$(git branch --show-current 2>/dev/null || true)"
if [[ -z "$current_branch" ]]; then
    echo "[错误] 当前未检测到有效分支，可能处于 detached HEAD 状态"
    echo "[提示] 请将以上完整输出截图或复制，联系程序组处理"
    exit 1
fi

run_cmd() {
    local step_name="$1"
    shift

    local stdout_file
    local stderr_file
    stdout_file="$(mktemp)"
    stderr_file="$(mktemp)"

    echo
    echo "===== ${step_name} ====="

    if "$@" >"$stdout_file" 2>"$stderr_file"; then
        if [[ -s "$stdout_file" ]]; then
            cat "$stdout_file"
        fi
    else
        if [[ -s "$stdout_file" ]]; then
            cat "$stdout_file"
        fi

        echo
        echo "----- Git 报错信息 -----"
        if [[ -s "$stderr_file" ]]; then
            cat "$stderr_file"
        else
            echo "[无额外 stderr 输出]"
        fi

        echo
        echo "[提示] 请将以上完整报错信息截图或复制，联系程序组处理"

        rm -f "$stdout_file" "$stderr_file"
        exit 1
    fi

    rm -f "$stdout_file" "$stderr_file"
}

echo
echo "===== 当前仓库 ====="
echo "$repo_root"

echo
echo "===== 当前分支 ====="
echo "$current_branch"

run_cmd "拉取最新代码" git pull --rebase --autostash

echo
echo "[完成] 拉取成功"