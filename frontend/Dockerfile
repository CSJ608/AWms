# AWms 前端静态镜像：仅托管构建产物
# dist 由 GitHub CI 构建 → artifact → deploy-test 下载后打进镜像（runner 无 node）
# 构建上下文 = frontend/
FROM docker.m.daocloud.io/library/nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY dist/ /usr/share/nginx/html/
EXPOSE 80