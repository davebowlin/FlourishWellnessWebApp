# This is used to import the sections, subsections, and questions into the db.
import tkinter as tk
from tkinter import filedialog, messagebox
import sqlite3
import csv
import os
import importlib


def table_exists(cur, db_type, table_name):
    if db_type == "sqlite":
        cur.execute(
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?",
            (table_name,),
        )
    elif db_type == "sqlserver":
        cur.execute(
            "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = ?",
            (table_name,),
        )
    else:
        raise ValueError(f"Unsupported database type: {db_type}")

    return cur.fetchone() is not None


def column_exists(cur, db_type, table_name, column_name):
    if db_type == "sqlite":
        cur.execute(f"PRAGMA table_info({table_name})")
        return any(row[1] == column_name for row in cur.fetchall())

    if db_type == "sqlserver":
        cur.execute(
            "SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = ? AND COLUMN_NAME = ?",
            (table_name, column_name),
        )
        return cur.fetchone() is not None

    raise ValueError(f"Unsupported database type: {db_type}")


def get_active_survey_entity_id(cur, db_type):
    if table_exists(cur, db_type, "SurveyEntities"):
        if column_exists(cur, db_type, "SurveyEntities", "Status"):
            cur.execute(
                (
                    "SELECT TOP 1 Id FROM SurveyEntities WHERE Status = ? ORDER BY Id DESC"
                    if db_type == "sqlserver"
                    else "SELECT Id FROM SurveyEntities WHERE Status = ? ORDER BY Id DESC LIMIT 1"
                ),
                (2,),
            )
            active = cur.fetchone()
            if active:
                return active[0]

        cur.execute(
            "SELECT TOP 1 Id FROM SurveyEntities ORDER BY Id DESC"
            if db_type == "sqlserver"
            else "SELECT Id FROM SurveyEntities ORDER BY Id DESC LIMIT 1"
        )
        latest = cur.fetchone()
        if latest:
            return latest[0]

    return None


def get_connection(db_type, db_target):
    if db_type == "sqlite":
        return sqlite3.connect(db_target)

    if db_type == "sqlserver":
        try:
            pyodbc = importlib.import_module("pyodbc")
        except ImportError as exc:
            raise RuntimeError("pyodbc is not installed. Run: pip install pyodbc")
        return pyodbc.connect(db_target)

    raise ValueError(f"Unsupported database type: {db_type}")


def import_csv_to_db(csv_path, db_type, db_target):
    """
    Import sections, subsections, and questions from CSV to database.

    CSV Format:
    Column A: Section Name (parent section)
    Column B: Subsection Name (leave empty if no subsection)
    Column C: Question Text

    Examples:
    "Mental Health","","Does your org have a mental health policy?"
    "Mental Health","Employee Support","Are EAP services available?"
    "Physical Wellness","","Is there a gym on site?"
    """
    conn = get_connection(db_type, db_target)
    cur = conn.cursor()

    if not table_exists(cur, db_type, "Sections"):
        raise RuntimeError("Target database is missing required table: Sections")
    if not table_exists(cur, db_type, "Questions"):
        raise RuntimeError("Target database is missing required table: Questions")

    survey_entity_id = get_active_survey_entity_id(cur, db_type)
    sections_have_survey_entity = column_exists(
        cur, db_type, "Sections", "SurveyEntityId"
    )
    questions_have_survey_entity = column_exists(
        cur, db_type, "Questions", "SurveyEntityId"
    )

    if sections_have_survey_entity and survey_entity_id is None:
        raise RuntimeError(
            "Sections requires SurveyEntityId, but no SurveyEntities row was found."
        )
    if questions_have_survey_entity and survey_entity_id is None:
        raise RuntimeError(
            "Questions requires SurveyEntityId, but no SurveyEntities row was found."
        )

    section_cache = {}  # Cache section IDs to avoid duplicate lookups
    stats = {
        "sections": 0,
        "subsections": 0,
        "questions": 0,
        "skipped": 0,
        "duplicate_questions": 0,
    }

    import itertools

    with open(csv_path, newline="", encoding="utf-8-sig") as csvfile:
        reader = csv.reader(csvfile)
        first_row = next(reader, None)
        # Always skip the first row if it matches the known headers (case-insensitive, strip whitespace)
        header_values = ["section", "subsection", "question"]
        is_header = False
        if first_row:
            normalized = [col.strip().lower() for col in first_row]
            if normalized == header_values:
                is_header = True
        if is_header:
            rows = reader  # skip header, process rest
        else:
            rows = itertools.chain([first_row], reader)  # process first row, then rest
        for row in rows:
            process_row(
                cur,
                row,
                section_cache,
                stats,
                db_type,
                survey_entity_id,
                sections_have_survey_entity,
                questions_have_survey_entity,
            )

    conn.commit()
    conn.close()

    return stats


def insert_section_and_get_id(
    cur,
    db_type,
    section_name,
    parent_section_id,
    survey_entity_id,
    sections_have_survey_entity,
):
    if db_type == "sqlserver":
        if sections_have_survey_entity:
            cur.execute(
                "INSERT INTO Sections (Name, ParentSectionId, SurveyEntityId) OUTPUT INSERTED.Id VALUES (?, ?, ?)",
                (section_name, parent_section_id, survey_entity_id),
            )
        else:
            cur.execute(
                "INSERT INTO Sections (Name, ParentSectionId) OUTPUT INSERTED.Id VALUES (?, ?)",
                (section_name, parent_section_id),
            )

        inserted = cur.fetchone()
        if not inserted or inserted[0] is None:
            raise RuntimeError(
                "Failed to retrieve inserted section id from SQL Server."
            )
        return inserted[0]

    if db_type == "sqlite":
        if sections_have_survey_entity:
            cur.execute(
                "INSERT INTO Sections (Name, ParentSectionId, SurveyEntityId) VALUES (?, ?, ?)",
                (section_name, parent_section_id, survey_entity_id),
            )
        else:
            cur.execute(
                "INSERT INTO Sections (Name, ParentSectionId) VALUES (?, ?)",
                (section_name, parent_section_id),
            )
        return cur.lastrowid

    raise ValueError(f"Unsupported database type: {db_type}")


def process_row(
    cur,
    row,
    section_cache,
    stats,
    db_type,
    survey_entity_id,
    sections_have_survey_entity,
    questions_have_survey_entity,
):
    """Process a single CSV row and insert section/subsection/question."""
    if len(row) < 1:
        stats["skipped"] += 1
        return  # Skip completely empty rows

    # Skip any row that matches the header (case-insensitive, ignores whitespace)
    header_values = ["section", "subsection", "question"]
    normalized = [col.strip().lower() for col in row]
    if normalized == header_values:
        stats["skipped"] += 1
        return

    section_name = row[0].strip()
    subsection_name = row[1].strip() if len(row) > 1 else ""
    question_text = row[2].strip() if len(row) > 2 else ""

    if not section_name:
        stats["skipped"] += 1
        return  # Skip rows without section name

    # Get or create parent section
    if section_name not in section_cache:
        if sections_have_survey_entity:
            cur.execute(
                "SELECT Id FROM Sections WHERE Name = ? AND ParentSectionId IS NULL AND SurveyEntityId = ?",
                (section_name, survey_entity_id),
            )
        else:
            cur.execute(
                "SELECT Id FROM Sections WHERE Name = ? AND ParentSectionId IS NULL",
                (section_name,),
            )
        section = cur.fetchone()
        if section:
            section_id = section[0]
        else:
            section_id = insert_section_and_get_id(
                cur,
                db_type,
                section_name,
                None,
                survey_entity_id,
                sections_have_survey_entity,
            )
            stats["sections"] += 1
        section_cache[section_name] = section_id
    else:
        section_id = section_cache[section_name]

    # Handle subsection if present
    target_section_id = section_id
    if subsection_name:
        subsection_key = f"{section_name}::{subsection_name}::{survey_entity_id}"
        if subsection_key not in section_cache:
            if sections_have_survey_entity:
                cur.execute(
                    "SELECT Id FROM Sections WHERE Name = ? AND ParentSectionId = ? AND SurveyEntityId = ?",
                    (subsection_name, section_id, survey_entity_id),
                )
            else:
                cur.execute(
                    "SELECT Id FROM Sections WHERE Name = ? AND ParentSectionId = ?",
                    (subsection_name, section_id),
                )
            subsection = cur.fetchone()
            if subsection:
                target_section_id = subsection[0]
            else:
                target_section_id = insert_section_and_get_id(
                    cur,
                    db_type,
                    subsection_name,
                    section_id,
                    survey_entity_id,
                    sections_have_survey_entity,
                )
                stats["subsections"] += 1
            section_cache[subsection_key] = target_section_id
        else:
            target_section_id = section_cache[subsection_key]

    # Insert question if provided (check for duplicates)
    if question_text:
        try:
            # Check if question already exists for this section
            cur.execute(
                "SELECT Id FROM Questions WHERE Text = ? AND SectionId = ?",
                (question_text, target_section_id),
            )
            existing = cur.fetchone()

            if existing:
                stats["duplicate_questions"] += 1
            else:
                if questions_have_survey_entity:
                    cur.execute(
                        "INSERT INTO Questions (Text, SurveyEntityId, SectionId) VALUES (?, ?, ?)",
                        (question_text, survey_entity_id, target_section_id),
                    )
                else:
                    cur.execute(
                        "INSERT INTO Questions (Text, SectionId) VALUES (?, ?)",
                        (question_text, target_section_id),
                    )
                stats["questions"] += 1
        except Exception as e:
            print(f"Error inserting question '{question_text}': {e}")
            stats["skipped"] += 1
    else:
        stats["skipped"] += 1


def select_and_import():
    csv_path = filedialog.askopenfilename(
        title="Select CSV File",
        filetypes=[("CSV Files", "*.csv"), ("All Files", "*.*")],
    )
    if not csv_path:
        return

    db_type = db_type_var.get()

    if db_type == "sqlite":
        db_path = db_path_var.get().strip()
        if not db_path or not os.path.exists(db_path):
            messagebox.showerror("Error", f"Database not found at {db_path}")
            return
        db_target = db_path
    else:
        db_target = sql_conn_var.get().strip()
        if not db_target:
            messagebox.showerror("Error", "Enter a SQL Server connection string.")
            return

    try:
        stats = import_csv_to_db(csv_path, db_type, db_target)
        message = f"""Import completed successfully!

Sections created: {stats['sections']}
Subsections created: {stats['subsections']}
Questions created: {stats['questions']}
Duplicate questions skipped: {stats['duplicate_questions']}
Rows skipped: {stats['skipped']}"""
        messagebox.showinfo("Success", message)
    except Exception as e:
        messagebox.showerror("Error", f"Import failed:\n{e}")


def test_connection():
    db_type = db_type_var.get()

    try:
        if db_type == "sqlite":
            db_path = db_path_var.get().strip()
            if not db_path or not os.path.exists(db_path):
                messagebox.showerror("Error", f"Database not found at {db_path}")
                return
            db_target = db_path
        else:
            db_target = sql_conn_var.get().strip()
            if not db_target:
                messagebox.showerror("Error", "Enter a SQL Server connection string.")
                return

        conn = get_connection(db_type, db_target)
        conn.close()
        messagebox.showinfo("Success", "Connection successful.")
    except Exception as e:
        messagebox.showerror("Error", f"Connection failed:\n{e}")


# Create GUI
root = tk.Tk()
root.title("FlourishWellness CSV Importer")
root.geometry("500x300")

title_label = tk.Label(
    root, text="Import Sections, Subsections & Questions", font=("Arial", 14, "bold")
)
title_label.pack(pady=15)

info_label = tk.Label(
    root,
    text="CSV Format:\nColumn A: Section | Column B: Subsection (optional) | Column C: Question",
    font=("Arial", 10),
    justify="left",
)
info_label.pack(pady=5)

# Database selection
db_frame = tk.Frame(root)
db_frame.pack(pady=10)

db_type_var = tk.StringVar(value="sqlserver")
db_path_var = tk.StringVar()
db_path_var.set("")
sql_conn_var = tk.StringVar(
    value="Driver={ODBC Driver 17 for SQL Server};Server=ASISQLDBPROD;Database=FlourishWellness;Trusted_Connection=yes;TrustServerCertificate=yes;"
)

db_type_frame = tk.Frame(root)
db_type_frame.pack(pady=5)

tk.Label(db_type_frame, text="Database Type:", font=("Arial", 10)).pack(
    side=tk.LEFT, padx=(0, 8)
)
tk.Radiobutton(db_type_frame, text="SQLite", variable=db_type_var, value="sqlite").pack(
    side=tk.LEFT
)
tk.Radiobutton(
    db_type_frame, text="SQL Server", variable=db_type_var, value="sqlserver"
).pack(side=tk.LEFT, padx=(10, 0))


def select_db_file():
    path = filedialog.askopenfilename(
        title="Select Database File",
        filetypes=[("SQLite DB", "*.db"), ("All Files", "*.*")],
    )
    if path:
        db_path_var.set(path)


db_label = tk.Label(db_frame, text="Database File:", font=("Arial", 10))
db_label.pack(side=tk.LEFT, padx=(0, 5))
db_entry = tk.Entry(db_frame, textvariable=db_path_var, width=40)
db_entry.pack(side=tk.LEFT, padx=(0, 5))
db_btn = tk.Button(db_frame, text="Browse", command=select_db_file)
db_btn.pack(side=tk.LEFT)

sql_frame = tk.Frame(root)
sql_frame.pack(pady=5)

sql_label = tk.Label(sql_frame, text="SQL Connection:", font=("Arial", 10))
sql_label.pack(side=tk.LEFT, padx=(0, 5))
sql_entry = tk.Entry(sql_frame, textvariable=sql_conn_var, width=52)
sql_entry.pack(side=tk.LEFT)


def update_db_input_state(*_):
    is_sqlite = db_type_var.get() == "sqlite"

    db_label.config(state=("normal" if is_sqlite else "disabled"))
    db_entry.config(state=("normal" if is_sqlite else "disabled"))
    db_btn.config(state=("normal" if is_sqlite else "disabled"))

    sql_label.config(state=("disabled" if is_sqlite else "normal"))
    sql_entry.config(state=("disabled" if is_sqlite else "normal"))


db_type_var.trace_add("write", update_db_input_state)
update_db_input_state()

import_btn = tk.Button(
    root,
    text="Select CSV and Import",
    command=select_and_import,
    font=("Arial", 12),
    width=25,
    bg="#007bff",
    fg="white",
    relief="raised",
    cursor="hand2",
)
import_btn.pack(pady=20)

test_btn = tk.Button(
    root,
    text="Test Connection",
    command=test_connection,
    font=("Arial", 10),
    width=20,
)
test_btn.pack(pady=(0, 10))

note_label = tk.Label(
    root,
    text="Note: Choose SQLite file or SQL Server connection string.",
    font=("Arial", 9),
    fg="gray",
)
note_label.pack(pady=5)

root.mainloop()
