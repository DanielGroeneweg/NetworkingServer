const express = require("express");
const app = express();

app.use(express.json());

let servers = [];

// Host registers
app.post("/register", (req, res) => {
    const ip = req.headers["x-forwarded-for"] || req.socket.remoteAddress;

    const server = {
        ip: req.body.ip,
        port: req.body.port,
        name: req.body.name,
        lastSeen: Date.now()
    };

    // Replace existing entry
    servers = servers.filter(s => s.ip !== ip);
    servers.push(server);

    console.log("Registered:", server);

    res.sendStatus(200);
});

// Get server list
app.get("/servers", (req, res) => {
    servers = servers.filter(s => Date.now() - s.lastSeen < 30000);
    res.json(servers);
});

// IMPORTANT: Railway uses dynamic port
const PORT = process.env.PORT || 3000;

app.listen(PORT, () => {
    console.log("Rendezvous running on port", PORT);
});