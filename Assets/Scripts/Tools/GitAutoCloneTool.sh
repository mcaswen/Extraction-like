#!/usr/bin/env bash
set -euo pipefail

show_step() {
    echo
    echo "===== $1 ====="
}

fail_with_message() {
    echo
    echo "----- 报错信息 -----"
    echo "$1"
    echo
    echo "[提示] 请把以上完整输出发给程序组"
    exit 1
}

have_cmd() {
    command -v "$1" >/dev/null 2>&1
}

run_cmd() {
    local title="$1"
    shift
    show_step "$title"
    "$@" || fail_with_message "命令执行失败：$*"
}

setup_brew_env() {
    if [[ -x /opt/homebrew/bin/brew ]]; then
        eval "$(/opt/homebrew/bin/brew shellenv)"
    elif [[ -x /usr/local/bin/brew ]]; then
        eval "$(/usr/local/bin/brew shellenv)"
    elif [[ -x /home/linuxbrew/.linuxbrew/bin/brew ]]; then
        eval "$(/home/linuxbrew/.linuxbrew/bin/brew shellenv)"
    fi
}

ensure_homebrew() {
    if have_cmd brew; then
        setup_brew_env
        return
    fi

    if [[ "$(uname -s)" == "Darwin" ]]; then
        if ! xcode-select -p >/dev/null 2>&1; then
            show_step "检测到未安装 Xcode Command Line Tools"
            xcode-select --install || true
            fail_with_message "已触发 Xcode Command Line Tools 安装。请先完成安装，然后重新运行脚本"
        fi
    fi

    show_step "安装 Homebrew"
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)" \
        || fail_with_message "Homebrew 安装失败。"

    setup_brew_env

    if ! have_cmd brew; then
        fail_with_message "Homebrew 安装完成后仍未找到 brew"
    fi
}

ensure_git_and_lfs() {
    local os_name
    os_name="$(uname -s)"

    if have_cmd git && git lfs version >/dev/null 2>&1; then
        return
    fi

    case "$os_name" in
        Darwin)
            ensure_homebrew
            run_cmd "通过 Homebrew 安装 Git 与 Git LFS" brew install git git-lfs
            ;;
        Linux)
            if have_cmd apt-get; then
                run_cmd "更新 apt 索引" sudo apt-get update
                run_cmd "安装 Git" sudo apt-get install -y git

                if ! git lfs version >/dev/null 2>&1; then
                    run_cmd "配置 Git LFS 软件源（deb）" bash -lc 'curl -fsSL https://packagecloud.io/install/repositories/github/git-lfs/script.deb.sh | sudo bash'
                    run_cmd "安装 Git LFS" sudo apt-get install -y git-lfs
                fi
            elif have_cmd dnf; then
                run_cmd "安装 Git" sudo dnf install -y git

                if ! git lfs version >/dev/null 2>&1; then
                    run_cmd "配置 Git LFS 软件源（rpm）" bash -lc 'curl -fsSL https://packagecloud.io/install/repositories/github/git-lfs/script.rpm.sh | sudo bash'
                    run_cmd "安装 Git LFS" sudo dnf install -y git-lfs
                fi
            else
                ensure_homebrew
                run_cmd "通过 Homebrew 安装 Git 与 Git LFS" brew install git git-lfs
            fi
            ;;
        *)
            fail_with_message "不支持的系统：$os_name"
            ;;
    esac
}

get_repo_name_from_url() {
    local repo_url="$1"
    local repo_name

    repo_name="${repo_url##*/}"
    repo_name="${repo_name%.git}"

    if [[ -z "$repo_name" ]]; then
        fail_with_message "无法从仓库地址推断本地文件夹名，请手动输入"
    fi

    echo "$repo_name"
}

main() {
    ensure_git_and_lfs

    run_cmd "验证 Git" git --version
    run_cmd "验证 Git LFS" git lfs version

    local current_name=""
    local current_email=""

    current_name="$(git config --global --get user.name || true)"
    current_email="$(git config --global --get user.email || true)"

    echo
    read -r -p "请输入 Git 用户名 [当前: ${current_name}]: " user_name
    if [[ -z "${user_name}" ]]; then
        user_name="${current_name}"
    fi

    read -r -p "请输入 Git 邮箱 [当前: ${current_email}]: " user_email
    if [[ -z "${user_email}" ]]; then
        user_email="${current_email}"
    fi

    if [[ -z "${user_name}" || -z "${user_email}" ]]; then
        fail_with_message "Git 用户名和邮箱不能为空"
    fi

    read -r -p "请输入远程仓库地址（HTTPS 或 SSH）: " repo_url
    if [[ -z "${repo_url}" ]]; then
        fail_with_message "远程仓库地址不能为空"
    fi

    local default_parent
    default_parent="$(pwd)"
    read -r -p "请输入克隆到的父目录 [默认: ${default_parent}]: " parent_dir
    if [[ -z "${parent_dir}" ]]; then
        parent_dir="${default_parent}"
    fi

    mkdir -p "${parent_dir}"

    local default_repo_name
    default_repo_name="$(get_repo_name_from_url "${repo_url}")"

    read -r -p "请输入本地文件夹名 [默认: ${default_repo_name}]: " repo_dir_name
    if [[ -z "${repo_dir_name}" ]]; then
        repo_dir_name="${default_repo_name}"
    fi

    local clone_path="${parent_dir%/}/${repo_dir_name}"
    if [[ -e "${clone_path}" ]]; then
        fail_with_message "目标目录已存在：${clone_path}"
    fi

    run_cmd "写入 Git 全局配置（用户名）" git config --global user.name "${user_name}"
    run_cmd "写入 Git 全局配置（邮箱）" git config --global user.email "${user_email}"
    run_cmd "初始化 Git LFS（全局）" git lfs install
    target_branch="dev"

    run_cmd "克隆远程仓库" git clone "${repo_url}" "${clone_path}"

    show_step "获取并切换到远程分支：${target_branch}"
    (
        cd "${clone_path}"
        git fetch origin "${target_branch}"
        git checkout -b "${target_branch}" "origin/${target_branch}"
        git lfs install --local
        git lfs pull
    ) || fail_with_message "切换 dev 分支或 Git LFS 初始化失败。"

    echo
    echo "[完成] 仓库已克隆到：${clone_path}"
    echo "[完成] Git / Git LFS / 用户配置已初始化完成"
}

main "$@"