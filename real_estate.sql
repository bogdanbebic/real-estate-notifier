CREATE TABLE IF NOT EXISTS RealEstate (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    TimestampIngested TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    Name TEXT NOT NULL,
    Price TEXT NOT NULL,
    Location TEXT NOT NULL,
    URL TEXT NOT NULL,
    Visited BOOLEAN DEFAULT 0 -- SQLite uses 0 (false) and 1 (true) for boolean values
);
