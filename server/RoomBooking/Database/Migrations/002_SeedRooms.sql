INSERT INTO rooms (name)
VALUES ('Room A'), ('Room B'), ('Room C')
ON CONFLICT (name) DO NOTHING;
