import tkinter as tk
from tkinter import messagebox
import sqlite3

def add_column_to_db(db_path, table_name, column_name, column_type):
    try:
        conn = sqlite3.connect(db_path)
        cursor = conn.cursor()
        
        alter_query = f"ALTER TABLE {table_name} ADD COLUMN {column_name} {column_type};"
        cursor.execute(alter_query)
        conn.commit()
        conn.close()
        return True, "Column added successfully."
    except sqlite3.Error as e:
        return False, str(e)

def submit_action():
    db_path = db_path_entry.get()
    table_name = table_name_entry.get()
    column_name = column_name_entry.get()
    column_type = column_type_entry.get()

    if not db_path or not table_name or not column_name or not column_type:
        messagebox.showerror("Error", "All fields are required.")
        return

    success, message = add_column_to_db(db_path, table_name, column_name, column_type)
    if success:
        messagebox.showinfo("Success", message)
    else:
        messagebox.showerror("Error", message)

# GUI setup
root = tk.Tk()
root.title("Add Column to Database")

# Labels and entries
tk.Label(root, text="Database Path:").grid(row=0, column=0, padx=10, pady=5, sticky="e")
db_path_entry = tk.Entry(root, width=40)
db_path_entry.grid(row=0, column=1, padx=10, pady=5)

tk.Label(root, text="Table Name:").grid(row=1, column=0, padx=10, pady=5, sticky="e")
table_name_entry = tk.Entry(root, width=40)
table_name_entry.grid(row=1, column=1, padx=10, pady=5)

tk.Label(root, text="Column Name:").grid(row=2, column=0, padx=10, pady=5, sticky="e")
column_name_entry = tk.Entry(root, width=40)
column_name_entry.grid(row=2, column=1, padx=10, pady=5)

tk.Label(root, text="Column Type:").grid(row=3, column=0, padx=10, pady=5, sticky="e")
column_type_entry = tk.Entry(root, width=40)
column_type_entry.grid(row=3, column=1, padx=10, pady=5)

# Submit button
tk.Button(root, text="Add Column", command=submit_action).grid(row=4, column=0, columnspan=2, pady=10)

root.mainloop()