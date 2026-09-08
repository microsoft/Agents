FROM python:3.12-slim AS builder

ARG PIP_INDEX_URL

ENV VIRTUAL_ENV=/opt/venv
RUN python -m venv "$VIRTUAL_ENV"
ENV PATH="$VIRTUAL_ENV/bin:$PATH"

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r requirements.txt

FROM python:3.12-slim

ENV PATH="/opt/venv/bin:$PATH" \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PORT=3978

RUN groupadd --system agent \
    && useradd --system --gid agent --home-dir /app agent

WORKDIR /app
COPY --from=builder /opt/venv /opt/venv
COPY src ./src

USER agent
EXPOSE 3978
CMD ["python", "-m", "src.main"]
