# Use Node.js for build
FROM node:18 AS build
WORKDIR /app

# Copy package files and install dependencies
COPY package.json package-lock.json ./
RUN npm install

# Copy source files and build the app
COPY . .
ARG MODE=development
RUN npm run build:$MODE

# Use Nginx to serve the built app
FROM nginx:alpine

# Copy the React build files
COPY --from=build /app/build /usr/share/nginx/html

# Add the custom Nginx configuration for React Router
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port and start Nginx
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

