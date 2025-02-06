#####################################
# 1) Build Stage for React (Vite)
#####################################
FROM node:18 AS build

# Set working directory
WORKDIR /app

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm install

# Copy the rest of the source code
COPY . .

# Build-time arguments (for Vite)
ARG MODE=production
ARG VITE_API_BASE_URL

# Inject them as environment variables so Vite can pick them up
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV MODE=$MODE

# Build the React app (Vite will read VITE_API_BASE_URL at build time)
RUN npm run build -- --mode $MODE


#####################################
# 2) Runtime Stage with Nginx
#####################################
FROM nginx:Alpine

# (Optional) Install debugging tools
RUN apt-get update && apt-get install -y \
    curl \
    net-tools \
    iputils-ping \
    procps \
    dnsutils \
    vim \
    telnet \
    && apt-get clean

# Copy custom Nginx config to override default site config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy the React app build output from the build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Expose ports (80 for HTTP, 443 if you choose to terminate TLS in this container)
EXPOSE 80 443

# Run Nginx in the foreground
CMD ["nginx", "-g", "daemon off;"]

