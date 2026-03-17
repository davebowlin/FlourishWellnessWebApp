# Use this to push some realistic test data into the database for development/testing purposes.
# It uses the ALF sites and users from AD. 
# THIS SHOULD ONLY BE RUN WHILE TESTING! THIS IS NOT MEANT FOR PRODUCTION USE AND MAY OVERWRITE REAL DATA IN THE DB.

import json
import random
import subprocess

import pyodbc

# Active Directory scope:
# americare.org -> Americare Systems Inc. -> Facilities -> Users -> ALF
AD_SEARCH_BASE = "OU=ALF,OU=Users,OU=Facilities,OU=Americare Systems Inc.,DC=americare,DC=org"
AD_NETBIOS_DOMAIN = "americare.org"

# SQL Server
DB_SERVER = "ASISQLDBPROD"
DB_DATABASE = "FlourishWellness"
DB_CONN_STR = (
    f"DRIVER={{ODBC Driver 17 for SQL Server}};"
    f"SERVER={DB_SERVER};DATABASE={DB_DATABASE};Trusted_Connection=yes;"
)

COMPLETE_USER_COUNT = 25
MAX_INCOMPLETE_USER_COUNT = 10
ANSWER_CHOICES = ["Fully Implemented", "Partially Implemented", "Not a Current Practice"]


def get_ad_users_via_powershell():
    """Returns all enabled AD users from the ALF OU as (username, display_name)."""
    ps_script = rf"""
Import-Module ActiveDirectory
$base = '{AD_SEARCH_BASE}'
$users = Get-ADUser -SearchBase $base -Filter 'Enabled -eq $true' -Properties DisplayName |
    Select-Object SamAccountName, DisplayName
$users | ConvertTo-Json -Depth 3 -Compress
"""
    result = subprocess.run(
        [
            "powershell",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            ps_script,
        ],
        capture_output=True,
        text=True,
        check=False,
    )

    if result.returncode != 0:
        raise RuntimeError(
            f"Failed to query AD.\nSTDOUT: {result.stdout.strip()}\nSTDERR: {result.stderr.strip()}"
        )

    raw = result.stdout.strip()
    if not raw:
        return []

    parsed = json.loads(raw)
    if isinstance(parsed, dict):
        parsed = [parsed]

    users = []
    for item in parsed:
        username = item.get("SamAccountName")
        display_name = item.get("DisplayName") or username
        if username:
            users.append((username, display_name))

    return users


def get_or_create_user(cursor, username, full_name):
    """Uses AD username (sAMAccountName) in Users.Email as unique identifier."""
    cursor.execute("SELECT Id FROM Users WHERE Email = ?", username)
    row = cursor.fetchone()
    if row:
        cursor.execute("UPDATE Users SET FullName = ? WHERE Id = ?", full_name, row.Id)
        return row.Id

    cursor.execute(
        "INSERT INTO Users (Email, FullName, PasswordHash, Role, IsSurveyCompleted, CreatedAt)"
        " OUTPUT INSERTED.Id VALUES (?, ?, '', ?, 0, GETUTCDATE())",
        username,
        full_name,
        1,
    )
    return cursor.fetchone().Id


def format_domain_username(sam_account_name):
    return f"{AD_NETBIOS_DOMAIN}\\{sam_account_name}"


def choose_answer_set(question_ids, is_complete):
    if is_complete:
        return question_ids

    if len(question_ids) == 1:
        return question_ids

    count = random.randint(1, len(question_ids) - 1)
    return random.sample(question_ids, count)


def populate_surveys():
    ad_users = get_ad_users_via_powershell()
    if len(ad_users) < COMPLETE_USER_COUNT:
        raise RuntimeError(
            f"Only found {len(ad_users)} eligible AD users in ALF OU; need at least {COMPLETE_USER_COUNT}."
        )

    # Always insert 25 complete users, plus a random 0..10 incomplete users.
    incomplete_user_count = random.randint(0, min(MAX_INCOMPLETE_USER_COUNT, len(ad_users) - COMPLETE_USER_COUNT))
    total_user_count = COMPLETE_USER_COUNT + incomplete_user_count

    selected_users = random.sample(ad_users, total_user_count)
    statuses = ([True] * COMPLETE_USER_COUNT) + ([False] * incomplete_user_count)
    combined = list(zip(selected_users, statuses))
    random.shuffle(combined)

    with pyodbc.connect(DB_CONN_STR) as db_conn:
        cursor = db_conn.cursor()

        cursor.execute("SELECT TOP 1 Id FROM SurveyEntities WHERE Status = 2 ORDER BY Year DESC")
        survey_row = cursor.fetchone()
        if not survey_row:
            raise RuntimeError("No active survey found (SurveyEntities.Status = 2).")
        survey_id = survey_row.Id

        cursor.execute("SELECT Id FROM Questions WHERE SurveyYear = ? ORDER BY Id", survey_id)
        question_ids = [r.Id for r in cursor.fetchall()]
        if not question_ids:
            raise RuntimeError(f"No questions found for active survey {survey_id}.")

        processed = []
        for (sam_account_name, full_name), is_complete in combined:
            username = format_domain_username(sam_account_name)
            user_id = get_or_create_user(cursor, username, full_name)

            cursor.execute("DELETE FROM Responses WHERE UserId = ? AND SurveyYear = ?", user_id, survey_id)

            answered_questions = choose_answer_set(question_ids, is_complete)
            for question_id in answered_questions:
                cursor.execute(
                    "INSERT INTO Responses (Answer, SurveyYear, QuestionId, UserId) VALUES (?, ?, ?, ?)",
                    random.choice(ANSWER_CHOICES),
                    survey_id,
                    question_id,
                    user_id,
                )

            cursor.execute(
                "SELECT 1 FROM UserSurveyStatuses WHERE UserId = ? AND SurveyYear = ?",
                user_id,
                survey_id,
            )
            if cursor.fetchone():
                cursor.execute(
                    "UPDATE UserSurveyStatuses SET IsCompleted = ?, UpdatedAt = GETUTCDATE()"
                    " WHERE UserId = ? AND SurveyYear = ?",
                    is_complete,
                    user_id,
                    survey_id,
                )
            else:
                cursor.execute(
                    "INSERT INTO UserSurveyStatuses (UserId, SurveyYear, IsCompleted, UpdatedAt)"
                    " VALUES (?, ?, ?, GETUTCDATE())",
                    user_id,
                    survey_id,
                    is_complete,
                )

            cursor.commit()
            processed.append(
                {
                    "username": username,
                    "full_name": full_name,
                    "is_complete": is_complete,
                    "answers_written": len(answered_questions),
                }
            )

    return survey_id, len(question_ids), processed


def main():
    survey_id, question_count, processed = populate_surveys()
    complete_count = sum(1 for p in processed if p["is_complete"])
    partial_count = len(processed) - complete_count

    print(f"Survey ID: {survey_id}")
    print(f"Questions in survey: {question_count}")
    print(f"Processed users: {len(processed)}")
    print(f"Complete: {complete_count} | Partial: {partial_count}")
    print("\nInserted usernames:")
    for p in processed:
        status = "Complete" if p["is_complete"] else "Partial"
        print(f"- {p['username']} ({status}, answers={p['answers_written']})")


if __name__ == "__main__":
    main()
