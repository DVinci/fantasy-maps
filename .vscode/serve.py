import http.server
import socketserver
import sys

PORT = 5500

class QuietHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass

class Server(socketserver.TCPServer):
    allow_reuse_address = True

with Server(("", PORT), QuietHandler) as httpd:
    sys.stdout.write(f"Listening on port {PORT}\n")
    sys.stdout.flush()
    httpd.serve_forever()
