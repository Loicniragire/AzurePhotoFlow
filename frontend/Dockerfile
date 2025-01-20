# Build Stage for React App
FROM node:18 AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm install

# Copy source code and build the app
COPY . .
ARG MODE=production
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build -- --mode $MODE

# Nginx Setup
FROM nginx:latest

# Install debugging tools
RUN apt-get update && apt-get install -y \
    curl \
    net-tools \
    iputils-ping \
    procps \
    dnsutils \
    vim \
    telnet \
    && apt-get clean

# Copy custom Nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the built React app from the build stage to Nginx
COPY --from=build /app/build /usr/share/nginx/html

# Expose ports (optional, but helpful for debugging locally)
EXPOSE 80 443

# Run Nginx in the foreground
CMD ["nginx", "-g", "daemon off;"]

