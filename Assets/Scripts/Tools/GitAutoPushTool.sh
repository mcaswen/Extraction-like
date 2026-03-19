#!/usr/bin/env bash
set -euo pipefail

# 允许提交的目录（相对于仓库根目录）
allowed_paths=("Assets/Art" "Assets/Audio" "Assets/Scenes" "Assets/SO" "Assets/Docs Assets/ThirdParty Assets/Prefabs Assets/Shader")

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$repo_root" ]]; then
    echo "[错误] 当前目录不在 Git 仓库中，或 Git 未正确安装"
    echo "[提示] 请将以上完整输出截图或复制，联系程序组处理"
    exit 1
fi

cd "$repo_root"

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

is_allowed() {
    local file_path="$1"
    for allowed in "${allowed_paths[@]}"; do
        if [[ "$file_path" == "$allowed/"* || "$file_path" == "$allowed" ]]; then
            return 0
        fi
    done
    return 1
}

run_cmd "拉取最新版本" git pull --rebase --autostash

ahead_count="0"
if git rev-parse --abbrev-ref --symbolic-full-name @{u} >/dev/null 2>&1; then
    ahead_count="$(git rev-list --count @{u}..HEAD 2>/dev/null)"
fi

if [[ "$ahead_count" != "0" ]]; then
    echo
    echo "===== 检测到未推送的本地提交 ====="
    echo "[提示] 当前分支有 ${ahead_count} 个本地提交尚未推送到远程"
    run_cmd "推送已有的本地提交" git push
fi

changed_files=()
while IFS= read -r line; do
    [[ -n "$line" ]] && changed_files+=("$line")
done < <(
    {
        git diff --name-only
        git diff --cached --name-only
        git ls-files --others --exclude-standard
    } | awk 'NF' | sort -u
)

if [[ ${#changed_files[@]} -eq 0 ]]; then
    echo
    echo "[提示] 当前没有可提交的改动"
    exit 0
fi

echo
echo "===== 当前检测到的改动概览 ====="
git status --short

echo
echo "===== 当前检测到的改动文件 ====="
printf '%s\n' "${changed_files[@]}"

outside_changes=()
for file_path in "${changed_files[@]}"; do
    if ! is_allowed "$file_path"; then
        outside_changes+=("$file_path")
    fi
done

stage_all=0

if [[ ${#outside_changes[@]} -gt 0 ]]; then
    echo
    echo "===== 检测到非目标文件夹改动 ====="
    printf '  - %s\n' "${outside_changes[@]}"
    echo
    echo "y = 回退这些非目标文件夹改动，只提交目标文件夹"
    echo "n = 保留这些改动，并一起提交"
    read -r -p "是否回退这些非目标文件夹的改动？(y/n): " user_choice

    if [[ "$user_choice" =~ ^[Yy]$ ]]; then
        echo
        echo "===== 正在回退非目标文件夹改动 ====="
        for file_path in "${outside_changes[@]}"; do
            if git ls-files --error-unmatch -- "$file_path" >/dev/null 2>&1; then
                run_cmd "回退文件：$file_path" git restore --staged --worktree -- "$file_path"
            else
                run_cmd "移除未跟踪文件或目录：$file_path" git clean -fd -- "$file_path"
            fi
        done
    else
        stage_all=1
        echo
        echo "[警告] 本次将一并提交非目标文件夹的改动"
        echo "[警告] 请在提交信息中说明原因"
    fi
fi

echo
echo "===== 暂存改动 ====="
if [[ "$stage_all" -eq 1 ]]; then
    run_cmd "暂存全部改动" git add -A
else
    stage_targets=()
    for allowed in "${allowed_paths[@]}"; do
        if [[ -e "$allowed" ]]; then
            stage_targets+=("$allowed")
        fi
    done

    if [[ ${#stage_targets[@]} -eq 0 ]]; then
        echo "[错误] 没有找到任何允许提交的目录，请检查脚本顶部 allowed_paths 配置"
        echo "[提示] 请将以上完整输出截图或复制，联系程序组处理"
        exit 1
    fi

    run_cmd "暂存目标目录改动" git add -A -- "${stage_targets[@]}"
fi

if git diff --cached --quiet; then
    echo
    echo "[提示] 暂存区没有可提交内容"
    exit 0
fi

echo
echo "===== 当前将要提交的改动概览 ====="
git status --short

echo
echo "===== 当前将要提交的所有文件 ====="
git diff --cached --name-only

echo
if [[ "$stage_all" -eq 1 ]]; then
    echo "[提示] 本次包含非目标文件夹改动，请在提交信息里写明原因"
fi

read -r -p "请输入提交信息: " commit_message
if [[ -z "$commit_message" ]]; then
    echo "[错误] 提交信息不能为空"
    exit 1
fi

run_cmd "创建提交" git commit -m "$commit_message"
run_cmd "推送到远程" git push

echo
echo "[完成] 提交并推送成功"